using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace WhisperPtt.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1001;

    // Modifiers for RegisterHotKey
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private readonly IntPtr _hwnd;
    private bool _registered;
    private bool _disposed;

    // Configured hotkey parameters
    private uint _targetVk;
    private uint _modifiers; // 0x0001 (Alt), 0x0002 (Ctrl), 0x0004 (Shift), 0x0008 (Win)

    private bool _isKeyPressed;

    public event Action? HotkeyDown;
    public event Action? HotkeyUp;

    // Obsolete event kept for backward compatibility if needed
    public event Action? HotkeyPressed;

    public HotkeyService(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public void Register(string hotkeyString)
    {
        Unregister();

        ParseHotkey(hotkeyString, out uint modifiers, out uint vk);
        _modifiers = modifiers;
        _targetVk = vk;

        // Register the hotkey with MOD_NOREPEAT so we only get one WM_HOTKEY press notification
        uint flags = _modifiers | MOD_NOREPEAT;
        _registered = RegisterHotKey(_hwnd, HOTKEY_ID, flags, _targetVk);
        if (!_registered)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Exception($"Не удалось зарегистрировать горячую клавишу '{hotkeyString}'. Код ошибки Win32: {error}");
        }
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
        _isKeyPressed = false;
    }

    /// <summary>
    /// Explicitly resets the internal key-pressed state.
    /// Useful for recovering from cancellation or unexpected states.
    /// </summary>
    public void ResetState()
    {
        _isKeyPressed = false;
    }

    public IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            handled = true; // Mark message as handled

            if (!_isKeyPressed)
            {
                _isKeyPressed = true;
                HotkeyDown?.Invoke();
                HotkeyPressed?.Invoke(); // legacy trigger
                StartPolling();
            }
        }
        return IntPtr.Zero;
    }

    private void StartPolling()
    {
        // Poll for key up (release) on a background thread so we don't block the UI thread.
        Task.Run(async () =>
        {
            try
            {
                while (_isKeyPressed)
                {
                    await Task.Delay(15);

                    // Read state of the target virtual key globally
                    short state = GetAsyncKeyState((int)_targetVk);
                    bool isDown = (state & 0x8000) != 0;

                    if (!isDown)
                    {
                        _isKeyPressed = false;

                        // Trigger HotkeyUp on the UI thread asynchronously (non-blocking)
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                HotkeyUp?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error invoking HotkeyUp: {ex.Message}");
                            }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in key state polling loop: {ex.Message}");
                _isKeyPressed = false; // Safeguard reset
            }
        });
    }

    private static void ParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    if (Enum.TryParse<Key>(part, ignoreCase: true, out var key))
                    {
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown key: '{part}' in hotkey string '{hotkeyString}'.");
                    }
                    break;
            }
        }

        if (vk == 0)
        {
            throw new ArgumentException($"No valid key found in hotkey string '{hotkeyString}'.");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Unregister();
            _disposed = true;
        }
    }
}
