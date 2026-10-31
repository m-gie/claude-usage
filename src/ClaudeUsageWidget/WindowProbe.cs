using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClaudeUsageWidget;

/// <summary>
/// Asks Windows (not WPF) what the user can actually see: whether the widget window is shown, cloaked
/// (e.g. on another virtual desktop), on a connected monitor, and whether the shell still has our tray icon.
/// Diagnostics only; every probe fails soft.
/// </summary>
internal static partial class WindowProbe
{
    private const int DWMWA_CLOAKED = 14;
    private const uint MONITOR_DEFAULTTONULL = 0;

    public static string Describe(nint hwnd)
    {
        if (hwnd == 0)
            return "window=none";

        GetWindowRect(hwnd, out var r);
        var cloaked = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int c, sizeof(int)) == 0 ? c.ToString() : "?";
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONULL) != 0 ? "yes" : "none";
        return $"window visible={IsWindowVisible(hwnd)} cloaked={cloaked} rect=({r.Left},{r.Top})-({r.Right},{r.Bottom}) on-monitor={monitor}";
    }

    /// <summary>Whether the shell still lists the tray icon. Finds NotifyIcon's private window and id by reflection.</summary>
    public static string DescribeTray(NotifyIcon icon)
    {
        try
        {
            var fields = typeof(NotifyIcon).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            var window = fields.Select(f => f.GetValue(icon)).OfType<NativeWindow>().FirstOrDefault();
            var idField = fields.FirstOrDefault(f => (f.FieldType == typeof(uint) || f.FieldType == typeof(int))
                                                     && f.Name.TrimStart('_').Equals("id", StringComparison.OrdinalIgnoreCase));
            if (window is null || idField is null)
                return "tray=unknown";

            var id = new NotifyIconIdentifier
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                hWnd = window.Handle,
                uID = Convert.ToUInt32(idField.GetValue(icon)),
            };
            var hr = Shell_NotifyIconGetRect(ref id, out _);
            return hr == 0 ? "tray=present" : $"tray=missing(0x{hr:X8})";
        }
        catch (Exception ex)
        {
            return $"tray=unknown({ex.GetType().Name})";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public Guid guidItem;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hWnd, out Rect rect);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hWnd, uint flags);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint hWnd, int attribute, out int value, int size);

    [LibraryImport("shell32.dll")]
    private static partial int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out Rect iconLocation);
}
