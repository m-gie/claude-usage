using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeUsageWidget.Core;
using Screen = System.Windows.Forms.Screen;

namespace ClaudeUsageWidget;

public partial class WidgetWindow : Window
{
    private static readonly Brush DimBrush = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
    private static readonly Brush WarningBrush = new SolidColorBrush(Palette.Orange);
    private static readonly Brush UnlockedBorder = new SolidColorBrush(Color.FromArgb(0x80, 0x5A, 0xA9, 0xFF));

    private bool _unlocked;
    private bool _placed;
    private Point? _savedPosition;

    /// <summary>Raised after the user finishes dragging the widget.</summary>
    public event Action<double, double>? PositionChanged;

    public WidgetWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindowStyles();
        Loaded += (_, _) => EnsureOnScreen();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    /// <summary>Position to restore on first show; validated against connected monitors.</summary>
    public void SetSavedPosition(double? left, double? top)
    {
        if (left is { } l && top is { } t)
            _savedPosition = new Point(l, t);
    }

    public nint Handle => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>When unlocked the widget takes clicks and can be dragged; otherwise clicks pass through.</summary>
    public bool Unlocked
    {
        get => _unlocked;
        set
        {
            _unlocked = value;
            Panel.BorderBrush = value ? UnlockedBorder : Brushes.Transparent;
            ApplyWindowStyles();
        }
    }

    public void Apply(DisplayState state)
    {
        Ring.Percent = state.Percent;
        Ring.RingBrush = new SolidColorBrush(Palette.For(state.Level));
        ResetText.Text = state.ResetLine;
        CountdownText.Text = state.CountdownLine;
        CountdownText.Visibility = state.CountdownLine.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        DetailText.Text = state.DetailLine ?? "";
        DetailText.Foreground = state.DetailIsWarning ? WarningBrush : DimBrush;
        DetailText.Visibility = state.DetailLine is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyWindowStyles()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0)
            return;

        const long always = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED;
        if (_unlocked)
            NativeMethods.SetExStyle(hwnd, always, NativeMethods.WS_EX_TRANSPARENT);
        else
            NativeMethods.SetExStyle(hwnd, always | NativeMethods.WS_EX_TRANSPARENT, 0);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_unlocked)
            return;
        DragMove();
        PositionChanged?.Invoke(Left, Top);
    }

    // A saved position can point at a monitor that is no longer connected.
    private void EnsureOnScreen()
    {
        if (_placed)
            return;
        _placed = true;

        var source = PresentationSource.FromVisual(this);
        var toDip = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var saved = _savedPosition ?? new Point(double.NaN, double.NaN);
        var probe = new Point(saved.X + ActualWidth / 2, saved.Y + ActualHeight / 2);
        var visible = _savedPosition is not null && Screen.AllScreens.Any(s =>
        {
            var area = s.WorkingArea;
            var topLeft = toDip.Transform(new Point(area.Left, area.Top));
            var bottomRight = toDip.Transform(new Point(area.Right, area.Bottom));
            return new Rect(topLeft, bottomRight).Contains(probe);
        });

        if (visible)
        {
            Left = saved.X;
            Top = saved.Y;
        }
        else
        {
            var work = SystemParameters.WorkArea;
            Left = work.Right - ActualWidth - 4;
            Top = work.Top + 4;
        }
    }
}
