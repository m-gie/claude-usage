using System.Runtime.InteropServices;

namespace ClaudeUsageWidget;

internal static partial class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_TOPMOST = 0x8;
    public const long WS_EX_TRANSPARENT = 0x20;
    public const long WS_EX_TOOLWINDOW = 0x80;
    public const long WS_EX_LAYERED = 0x80000;
    public const long WS_EX_NOACTIVATE = 0x08000000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string message);

    private const uint GW_HWNDPREV = 3;
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    private static readonly nint HWND_TOPMOST = -1;

    [LibraryImport("user32.dll")]
    private static partial nint GetWindow(nint hWnd, uint cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hWnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// Whether the window is really in the topmost band: it has WS_EX_TOPMOST and no visible
    /// non-topmost window sits above it in the z-order.
    /// </summary>
    public static bool IsTopmost(nint hwnd, out string detail)
    {
        if (((long)GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) == 0)
        {
            detail = "WS_EX_TOPMOST cleared";
            return false;
        }

        var above = 0;
        for (var h = GetWindow(hwnd, GW_HWNDPREV); h != 0; h = GetWindow(h, GW_HWNDPREV))
        {
            if (IsWindowVisible(h) && ((long)GetWindowLongPtr(h, GWL_EXSTYLE) & WS_EX_TOPMOST) == 0)
                above++;
        }
        detail = above == 0 ? "" : $"{above} non-topmost window(s) above";
        return above == 0;
    }

    /// <summary>Moves the window to the top of the topmost band without activating it.</summary>
    public static bool MakeTopmost(nint hwnd) =>
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    public static void SetExStyle(nint hwnd, long add, long remove)
    {
        var style = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)((style | add) & ~remove));
    }
}
