using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using WhisperPtt.Models;

namespace WhisperPtt.ViewModels;

public class ModelItem
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
}

public class LanguageItem
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class TimeoutItem
{
    public int Value { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class SettingsViewModel : ViewModelBase
{


    private string _selectedModel = string.Empty;
    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    private ModelItem? _selectedModelItem;
    public ModelItem? SelectedModelItem
    {
        get => _selectedModelItem;
        set
        {
            if (SetProperty(ref _selectedModelItem, value) && value != null)
            {
                SelectedModel = value.Name;
            }
        }
    }

    private string _selectedLanguage = "ru";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    private LanguageItem? _selectedLanguageItem;
    public LanguageItem? SelectedLanguageItem
    {
        get => _selectedLanguageItem;
        set
        {
            if (SetProperty(ref _selectedLanguageItem, value) && value != null)
            {
                SelectedLanguage = value.Code;
            }
        }
    }

    private string _customPrompt = string.Empty;
    public string CustomPrompt
    {
        get => _customPrompt;
        set => SetProperty(ref _customPrompt, value);
    }

    private int _unloadTimeoutMinutes = 10;
    public int UnloadTimeoutMinutes
    {
        get => _unloadTimeoutMinutes;
        set => SetProperty(ref _unloadTimeoutMinutes, value);
    }

    private TimeoutItem? _selectedTimeoutItem;
    public TimeoutItem? SelectedTimeoutItem
    {
        get => _selectedTimeoutItem;
        set
        {
            if (SetProperty(ref _selectedTimeoutItem, value) && value != null)
            {
                UnloadTimeoutMinutes = value.Value;
            }
        }
    }

    private string _hotkey = "F2";
    public string Hotkey
    {
        get => _hotkey;
        set
        {
            if (SetProperty(ref _hotkey, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplay));
            }
        }
    }

    public string HotkeyDisplay => string.IsNullOrEmpty(Hotkey) ? "Нажмите клавишу..." : Hotkey;

    private bool _isCapturingHotkey;
    public bool IsCapturingHotkey
    {
        get => _isCapturingHotkey;
        set => SetProperty(ref _isCapturingHotkey, value);
    }

    private string _selectedAudioDevice = "Системный по умолчанию";
    public string SelectedAudioDevice
    {
        get => _selectedAudioDevice;
        set => SetProperty(ref _selectedAudioDevice, value);
    }

    public ObservableCollection<ModelItem> AvailableModels { get; } = new();
    public ObservableCollection<string> AvailableAudioDevices { get; } = new();

    public List<LanguageItem> AvailableLanguages { get; } = new()
    {
        new LanguageItem { Code = "ru", DisplayName = "Русский" },
        new LanguageItem { Code = "en", DisplayName = "English" },
        new LanguageItem { Code = "de", DisplayName = "Deutsch" },
        new LanguageItem { Code = "fr", DisplayName = "Français" },
        new LanguageItem { Code = "es", DisplayName = "Español" },
        new LanguageItem { Code = "ja", DisplayName = "日本語" },
        new LanguageItem { Code = "zh", DisplayName = "中文" },
    };

    public List<TimeoutItem> AvailableTimeouts { get; } = new()
    {
        new TimeoutItem { Value = 0, DisplayName = "Никогда" },
        new TimeoutItem { Value = 5, DisplayName = "5 минут" },
        new TimeoutItem { Value = 10, DisplayName = "10 минут" },
        new TimeoutItem { Value = 15, DisplayName = "15 минут" },
        new TimeoutItem { Value = 30, DisplayName = "30 минут" },
        new TimeoutItem { Value = 60, DisplayName = "60 минут" },
    };

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action? SettingsSaved;
    public event Action? CloseRequested;

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke());

        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var settings = AppSettings.Instance;

        // Populate available models and check download status
        var modelsDir = AppSettings.ModelsDirectory;
        foreach (var kvp in AppSettings.ModelFileNames)
        {
            var modelPath = Path.Combine(modelsDir, kvp.Value);
            var isDownloaded = File.Exists(modelPath);
            var statusText = isDownloaded ? "установлена" : "требует загрузки";
            AvailableModels.Add(new ModelItem
            {
                Name = kvp.Key,
                DisplayName = $"{kvp.Key} ({statusText})",
                IsDownloaded = isDownloaded
            });
        }

        // Set current selections from settings
        SelectedModel = settings.Model;
        SelectedModelItem = AvailableModels.FirstOrDefault(m => m.Name == settings.Model)
                            ?? AvailableModels.FirstOrDefault();

        SelectedLanguage = settings.Language;
        SelectedLanguageItem = AvailableLanguages.FirstOrDefault(l => l.Code == settings.Language)
                               ?? AvailableLanguages.First();

        CustomPrompt = settings.CustomPrompt;
        Hotkey = settings.Hotkey;

        UnloadTimeoutMinutes = settings.UnloadTimeoutMinutes;
        SelectedTimeoutItem = AvailableTimeouts.FirstOrDefault(t => t.Value == settings.UnloadTimeoutMinutes)
                              ?? AvailableTimeouts.FirstOrDefault(t => t.Value == 10);

        // Populate audio devices
        AvailableAudioDevices.Clear();
        foreach (var device in WhisperPtt.Services.AudioService.GetInputDevices())
        {
            AvailableAudioDevices.Add(device);
        }

        SelectedAudioDevice = settings.SelectedAudioDevice;
        if (!AvailableAudioDevices.Contains(SelectedAudioDevice))
        {
            SelectedAudioDevice = AvailableAudioDevices.FirstOrDefault() ?? "Системный по умолчанию";
        }
    }

    private void Save()
    {
        var settings = AppSettings.Instance;
        settings.Model = SelectedModel;
        settings.Language = SelectedLanguage;
        settings.CustomPrompt = CustomPrompt;
        settings.Hotkey = Hotkey;
        settings.UnloadTimeoutMinutes = UnloadTimeoutMinutes;
        settings.SelectedAudioDevice = SelectedAudioDevice;
        settings.Save();

        SettingsSaved?.Invoke();
        CloseRequested?.Invoke();
    }

    public void CaptureHotkey(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
    {
        // Ignore modifier-only presses
        if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
            key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
            key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
            key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin ||
            key == System.Windows.Input.Key.System)
        {
            return;
        }

        var parts = new List<string>();

        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
            parts.Add("Win");

        parts.Add(key.ToString());

        Hotkey = string.Join("+", parts);
        IsCapturingHotkey = false;
    }
}
