using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClaudeUsageWidget;

/// <summary>System-wide hotkey via RegisterHotKey, delivered to an existing window's message loop.</summary>
public sealed partial class HotkeyManager : IDisposable
{
    private const int HotkeyId = 1;
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    private HwndSource? _source;
    private Action? _onPressed;

    public bool Register(nint hwnd, uint modifiers, uint virtualKey, Action onPressed)
    {
        if (!RegisterHotKey(hwnd, HotkeyId, modifiers | MOD_NOREPEAT, virtualKey))
            return false;

        _onPressed = onPressed;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(WndProc);
        return true;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam == HotkeyId)
        {
            _onPressed?.Invoke();
            handled = true;
        }
        return 0;
    }

    public void Dispose()
    {
        if (_source is null)
            return;
        _source.RemoveHook(WndProc);
        UnregisterHotKey(_source.Handle, HotkeyId);
        _source = null;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(nint hWnd, int id);
}
