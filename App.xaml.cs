using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using WhisperPtt.Helpers;
using WhisperPtt.Models;
using WhisperPtt.Services;
using WhisperPtt.ViewModels;
using WhisperPtt.Views;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using ToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace WhisperPtt;

/// <summary>
/// Application entry point. Manages tray icon, services lifecycle, and the
/// hotkey-driven record → transcribe → paste workflow.
/// </summary>
public partial class App : System.Windows.Application
{
    private NotifyIcon? _notifyIcon;
    private HotkeyService? _hotkeyService;
    private AudioService? _audioService;
    private WhisperService? _whisperService;
    private WidgetWindow? _widgetWindow;
    private WidgetViewModel? _widgetVm;
    private SettingsWindow? _settingsWindow;

    private bool _isRecording;
    private bool _isProcessing; // guards against double-press during transcription
    private System.Threading.CancellationTokenSource? _transcriptionCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load persisted settings
        AppSettings.Load();

        // Create services (hotkey service needs hwnd — we'll wire it after widget window is created)
        _audioService = new AudioService();
        _whisperService = new WhisperService();

        // Create widget
        _widgetVm = new WidgetViewModel();
        _widgetWindow = new WidgetWindow { DataContext = _widgetVm };

        // Start hidden
        _widgetVm.CurrentState = WidgetState.Hidden;
        _widgetWindow.Show();
        _widgetWindow.Visibility = Visibility.Hidden;

        // Create HotkeyService now that widget window has an HWND
        var hwndSource = PresentationSource.FromVisual(_widgetWindow) as HwndSource;
        if (hwndSource != null)
        {
            _hotkeyService = new HotkeyService(hwndSource.Handle);
            hwndSource.AddHook(_hotkeyService.WndProc);
            _hotkeyService.HotkeyDown += OnHotkeyDown;
            _hotkeyService.HotkeyUp += OnHotkeyUp;
            RegisterCurrentHotkey();
        }

        // Subscribe to property changes to manage window visibility and animations
        _widgetVm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(WidgetViewModel.IsVisible))
            {
                Dispatcher.Invoke(() =>
                {
                    _widgetWindow.Visibility = _widgetVm.IsVisible
                        ? Visibility.Visible
                        : Visibility.Hidden;
                });
            }
            else if (args.PropertyName == nameof(WidgetViewModel.CurrentState))
            {
                Dispatcher.Invoke(() => _widgetWindow.OnStateChanged());
            }
        };

        // Subscribe to audio RMS level updates
        _audioService.RmsCalculated += level =>
        {
            Dispatcher.Invoke(() => _widgetVm.RmsLevel = level);
        };

        // Set up tray icon
        SetupNotifyIcon();
    }

    private void SetupNotifyIcon()
    {
        Icon? trayIcon = SystemIcons.Application;
        try
        {
            var iconUri = new Uri("pack://application:,,,/WhisperPtt;component/app_icon.ico", UriKind.RelativeOrAbsolute);
            var sri = System.Windows.Application.GetResourceStream(iconUri);
            if (sri != null)
            {
                using (var stream = sri.Stream)
                {
                    trayIcon = new Icon(stream);
                }
            }
        }
        catch
        {
            // Fallback gracefully to default system icon
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "WhisperPtt",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Настройки");
        settingsItem.Click += (_, _) => Dispatcher.Invoke(ShowSettings);

        var exitItem = new ToolStripMenuItem("Выйти");
        exitItem.Click += (_, _) => Dispatcher.Invoke(() => Shutdown());

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
    }

    private void ShowSettings()
    {
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.ViewModel.SettingsSaved += OnSettingsSaved;
        _settingsWindow.ShowDialog();
    }

    private void OnSettingsSaved()
    {
        // Re-register hotkey with new settings
        RegisterCurrentHotkey();
    }

    private void RegisterCurrentHotkey()
    {
        if (_hotkeyService == null) return;

        _hotkeyService.Unregister();

        var settings = AppSettings.Instance;
        try
        {
            _hotkeyService.Register(settings.Hotkey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register hotkey: {ex.Message}");
        }
    }

    private async void OnHotkeyDown()
    {
        if (_isProcessing)
        {
            // Cancel ongoing transcription!
            try
            {
                _transcriptionCts?.Cancel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error canceling transcription: {ex.Message}");
            }

            // Immediately reset UI and state for instant responsiveness!
            _isProcessing = false;
            _transcriptionCts = null;
            _hotkeyService?.ResetState();
            if (_widgetVm != null)
            {
                _widgetVm.CurrentState = WidgetState.Hidden;
            }
            return;
        }

        // Guard: if currently downloading or loading the model, ignore
        if (_widgetVm != null && (_widgetVm.CurrentState == WidgetState.Downloading || _widgetVm.CurrentState == WidgetState.Loading))
        {
            return;
        }

        if (!_isRecording)
        {
            await StartRecordingFlowAsync();
        }
    }

    private async void OnHotkeyUp()
    {
        if (_isProcessing) return;

        // Guard: only stop recording if we are actually recording audio
        if (!_isRecording || (_widgetVm != null && _widgetVm.CurrentState != WidgetState.Recording))
        {
            return;
        }

        await StopRecordingAndTranscribeAsync();
    }

    /// <summary>
    /// First hotkey press: ensure model is ready, then start recording.
    /// </summary>
    private async Task StartRecordingFlowAsync()
    {
        _isRecording = true;

        try
        {
            var settings = AppSettings.Instance;
            var fileName = AppSettings.ResolveModelFileName(settings.SelectedModel);
            var modelPath = System.IO.Path.Combine(AppSettings.GetModelsDir(), fileName);

            // Check if model needs downloading
            if (!System.IO.File.Exists(modelPath))
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _widgetVm!.DownloadProgress = 0;
                    _widgetVm.CurrentState = WidgetState.Downloading;
                });

                // Subscribe to download progress
                void OnProgress(int p) => Dispatcher.Invoke(() => _widgetVm!.DownloadProgress = p);
                _whisperService!.DownloadProgress += OnProgress;

                try
                {
                    await _whisperService.EnsureModelAsync(settings.SelectedModel, CancellationToken.None);
                }
                finally
                {
                    _whisperService.DownloadProgress -= OnProgress;
                }
            }

            // Load model if not yet loaded
            if (!_whisperService!.IsModelLoaded)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _widgetVm!.CurrentState = WidgetState.Loading;
                });

                await _whisperService.LoadModelAsync(settings.SelectedModel);
            }

            _audioService!.StartRecording(settings.SelectedAudioDevice);

            await Dispatcher.InvokeAsync(() =>
            {
                _widgetVm!.CurrentState = WidgetState.Recording;
            });
        }
        catch (Exception ex)
        {
            _isRecording = false;
            _hotkeyService?.ResetState();
            await Dispatcher.InvokeAsync(() =>
            {
                _widgetVm!.ErrorText = ex.Message;
                _widgetVm.CurrentState = WidgetState.Error;
            });

            // Auto-hide error after 3 seconds
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() => _widgetVm!.CurrentState = WidgetState.Hidden);
            });
        }
    }

    /// <summary>
    /// Second hotkey press: stop recording, transcribe, paste, hide.
    /// </summary>
    private async Task StopRecordingAndTranscribeAsync()
    {
        _isProcessing = true;
        _isRecording = false;

        var cts = new System.Threading.CancellationTokenSource();
        _transcriptionCts = cts;

        try
        {
            // Stop recording and get audio data
            var audioData = _audioService!.StopRecording();

            await Dispatcher.InvokeAsync(() =>
            {
                _widgetVm!.CurrentState = WidgetState.Transcribing;
            });

            // Transcribe
            var settings = AppSettings.Instance;
            var text = await Task.Run(async () => await _whisperService!.TranscribeAsync(
                audioData,
                settings.SelectedLanguage,
                settings.GetEffectivePrompt(),
                cts.Token));

            // If this specific session was canceled, abort without modifying UI/pasting
            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            // Reset unload timer
            _whisperService!.ResetUnloadTimer(settings.UnloadTimeoutMinutes);

            // Paste via InputService
            if (!string.IsNullOrWhiteSpace(text))
            {
                await InputService.TypeTextAsync(text);
            }

            // Hide widget
            await Dispatcher.InvokeAsync(() =>
            {
                _widgetVm!.CurrentState = WidgetState.Hidden;
            });
        }
        catch (OperationCanceledException)
        {
            // Gracefully handled
        }
        catch (Exception ex)
        {
            if (!cts.Token.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _widgetVm!.ErrorText = ex.Message;
                    _widgetVm.CurrentState = WidgetState.Error;
                });

                _ = Task.Delay(3000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => _widgetVm!.CurrentState = WidgetState.Hidden);
                });
            }
        }
        finally
        {
            // Reset active states only if this session remains active
            if (_transcriptionCts == cts)
            {
                _isProcessing = false;
                _transcriptionCts = null;
                _hotkeyService?.ResetState();
            }
            cts.Dispose();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _audioService?.Dispose();
        _whisperService?.Dispose();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.OnExit(e);
    }
}
