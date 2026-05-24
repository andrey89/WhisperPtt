using WhisperPtt.Helpers;
using Clipboard = System.Windows.Clipboard;

namespace WhisperPtt.Services;

public static class InputService
{
    /// <summary>
    /// Types text into the currently focused application by sending direct unicode inputs (SendInput).
    /// Bypasses the clipboard completely for maximum speed, compatibility, and safety.
    /// </summary>
    public static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Win32Helper.SendUnicodeString(text);
    }

    /// <summary>
    /// Async version that adds a short delay to allow target window focus to stabilize
    /// after the hotkey release.
    /// </summary>
    public static async Task TypeTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // Give the target application 50ms to finish processing the hotkey release (F2) and stabilize focus
        await Task.Delay(50);

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => TypeText(text));
    }
}
