using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget;

/// <summary>Tray icon showing a mini ring in the level colour, the usage tooltip and the app menu.</summary>
public sealed partial class TrayIcon : IDisposable
{
    private const int NotifyIconTextLimit = 127;

    private readonly NotifyIcon _icon;
    private nint _hicon;

    public TrayIcon(Action toggle, Func<bool> isVisible, Action refresh,
        Func<bool> getUnlocked, Action<bool> setUnlocked,
        Func<bool> getAutostart, Action<bool> setAutostart, Action quit)
    {
        var showHide = new ToolStripMenuItem("Show widget", null, (_, _) => toggle());
        var refreshItem = new ToolStripMenuItem("Refresh now", null, (_, _) => refresh());
        var unlock = new ToolStripMenuItem("Unlock position", null, (_, _) => setUnlocked(!getUnlocked()));
        var autostart = new ToolStripMenuItem("Start with Windows", null, (_, _) => setAutostart(!getAutostart()));
        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => quit());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([showHide, refreshItem, unlock, autostart, new ToolStripSeparator(), quitItem]);
        menu.Opening += (_, _) =>
        {
            showHide.Text = isVisible() ? "Hide widget" : "Show widget";
            unlock.Checked = getUnlocked();
            autostart.Checked = getAutostart();
        };

        _icon = new NotifyIcon { ContextMenuStrip = menu, Text = "Claude: Loading…", Visible = true };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                toggle();
        };
        SetIcon(null, Level.Inactive);
    }

    public void Apply(DisplayState state)
    {
        _icon.Text = state.Tooltip.Length > NotifyIconTextLimit ? state.Tooltip[..NotifyIconTextLimit] : state.Tooltip;
        SetIcon(state.Percent, state.Level);
    }

    public void ShowBalloon(string text) => _icon.ShowBalloonTip(5000, "Claude Usage", text, ToolTipIcon.Warning);

    private void SetIcon(double? percent, Level level)
    {
        var size = SystemInformation.SmallIconSize.Width >= 24 ? 32 : 16;
        var c = Palette.For(level);
        var color = Color.FromArgb(c.R, c.G, c.B);
        var thickness = size / 5f;
        var rect = new RectangleF(thickness / 2, thickness / 2, size - thickness, size - thickness);

        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var track = new Pen(Color.FromArgb(90, 128, 128, 128), thickness);
            g.DrawEllipse(track, rect);
            var sweep = (float)(Math.Clamp(percent ?? 0, 0, 100) / 100 * 360);
            if (sweep > 0)
            {
                using var arc = new Pen(color, thickness);
                g.DrawArc(arc, rect, -90, sweep);
            }
        }

        var old = _hicon;
        _hicon = bitmap.GetHicon();
        _icon.Icon = Icon.FromHandle(_hicon);
        if (old != 0)
            DestroyIcon(old);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        if (_hicon != 0)
            DestroyIcon(_hicon);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint hIcon);
}
