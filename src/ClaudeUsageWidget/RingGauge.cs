using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ClaudeUsageWidget;

/// <summary>Circular meter with the percentage in the middle.</summary>
public sealed class RingGauge : FrameworkElement
{
    private const double Thickness = 6;

    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double?), typeof(RingGauge),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(RingGauge),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Pen TrackPen = new(new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)), Thickness);

    static RingGauge() => TrackPen.Freeze();

    public double? Percent
    {
        get => (double?)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public Brush RingBrush
    {
        get => (Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(54, 54);

    protected override void OnRender(DrawingContext dc)
    {
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Min(ActualWidth, ActualHeight) / 2 - Thickness / 2;
        dc.DrawEllipse(null, TrackPen, center, radius, radius);

        var fraction = Math.Clamp((Percent ?? 0) / 100, 0, 1);
        var pen = new Pen(RingBrush, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (fraction >= 1)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
        }
        else if (fraction > 0)
        {
            var angle = fraction * 2 * Math.PI;
            var start = new Point(center.X, center.Y - radius);
            var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(start, isFilled: false, isClosed: false);
                ctx.ArcTo(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            }
            dc.DrawGeometry(null, pen, geometry);
        }

        var label = Percent is { } p ? $"{Math.Round(p).ToString(CultureInfo.InvariantCulture)}%" : "–";
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            13, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }
}
