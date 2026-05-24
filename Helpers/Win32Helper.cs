using System.Runtime.InteropServices;

namespace WhisperPtt.Helpers;

public static class Win32Helper
{
    // Hotkey registration
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Window styles for WS_EX_NOACTIVATE
    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // SendInput for Ctrl+V simulation
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // Constants
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WM_HOTKEY = 0x0312;

    // Input type constants
    private const uint INPUT_KEYBOARD = 1;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    /// <summary>
    /// Types a string of unicode characters directly to the active focused window using SendInput.
    /// Bypasses the clipboard completely and works reliably across different keyboard layouts.
    /// </summary>
    public static void SendUnicodeString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var inputs = new INPUT[text.Length * 2];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            // Key down
            inputs[i * 2] = CreateUnicodeKeyInput(c, 0);
            // Key up
            inputs[i * 2 + 1] = CreateUnicodeKeyInput(c, KEYEVENTF_KEYUP);
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Simulates Ctrl+V keystroke via SendInput: Ctrl down, V down, V up, Ctrl up.
    /// </summary>
    public static void SimulateCtrlV()
    {
        var inputs = new INPUT[4];

        // Ctrl down
        inputs[0] = CreateKeyInput(VK_CONTROL, 0);
        // V down
        inputs[1] = CreateKeyInput(VK_V, 0);
        // V up
        inputs[2] = CreateKeyInput(VK_V, KEYEVENTF_KEYUP);
        // Ctrl up
        inputs[3] = CreateKeyInput(VK_CONTROL, KEYEVENTF_KEYUP);

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Makes a window non-activatable and hidden from taskbar (tool window).
    /// </summary>
    public static void MakeWindowNoActivate(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private static INPUT CreateKeyInput(ushort vk, uint flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private static INPUT CreateUnicodeKeyInput(char c, uint flags)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = flags | KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct INPUT
{
    public uint type;
    public InputUnion u;
}

[StructLayout(LayoutKind.Explicit)]
public struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
public struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}
