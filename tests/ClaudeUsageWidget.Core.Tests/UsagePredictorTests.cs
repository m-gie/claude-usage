using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public class UsagePredictorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Reset = new(2026, 10, 30, 17, 0, 0, TimeSpan.Zero);

    private static SessionUsage U(double percent, DateTimeOffset? reset = null) => new(percent, reset ?? Reset);

    [Fact]
    public void No_prediction_with_single_sample()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(40));

        Assert.Null(p.PredictFull());
    }

    [Fact]
    public void No_prediction_below_two_points()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(40));
        p.Add(T0.AddMinutes(20), U(41));

        Assert.Null(p.PredictFull());
    }

    [Fact]
    public void Predicts_linear()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(40));
        p.Add(T0.AddMinutes(20), U(50));

        Assert.Equal(new DateTimeOffset(2026, 10, 30, 16, 0, 0, TimeSpan.Zero), p.PredictFull());
    }

    [Fact]
    public void No_prediction_within_five_minutes()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(40));
        p.Add(T0.AddMinutes(4), U(45));

        Assert.Null(p.PredictFull());
    }

    [Fact]
    public void Prediction_after_reset_is_still_returned()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(40));
        p.Add(T0.AddMinutes(20), U(42));

        // 0.1%/min from 42% at T0+20m: 580 more minutes, well past the reset; the display decides what to show.
        Assert.Equal(T0.AddMinutes(600), p.PredictFull());
    }

    [Fact]
    public void Old_samples_dropped()
    {
        // With the 14:00 sample the pace (0.64%/min) would predict ~15:20; without it, 0.2%/min from 42% gives 19:40.
        var p = new UsagePredictor();
        p.Add(T0, U(10));
        p.Add(T0.AddMinutes(40), U(40));
        p.Add(T0.AddMinutes(50), U(42));

        Assert.Equal(T0.AddMinutes(340), p.PredictFull());
    }

    [Fact]
    public void New_window_clears_history()
    {
        var newReset = new DateTimeOffset(2026, 10, 30, 22, 0, 0, TimeSpan.Zero);
        var p = new UsagePredictor();
        p.Add(T0, U(80));
        p.Add(T0.AddMinutes(20), U(2, newReset));
        p.Add(T0.AddMinutes(40), U(12, newReset));

        Assert.Equal(new DateTimeOffset(2026, 10, 30, 17, 36, 0, TimeSpan.Zero), p.PredictFull());
    }

    [Fact]
    public void Reset_jitter_keeps_history()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(40, Reset.AddMilliseconds(100)));
        p.Add(T0.AddMinutes(20), U(50, Reset.AddMilliseconds(900)));

        Assert.Equal(new DateTimeOffset(2026, 10, 30, 16, 0, 0, TimeSpan.Zero), p.PredictFull());
    }

    [Fact]
    public void No_prediction_without_reset_time()
    {
        var p = new UsagePredictor();
        p.Add(T0, new SessionUsage(40, null));
        p.Add(T0.AddMinutes(20), new SessionUsage(50, null));

        Assert.Null(p.PredictFull());
    }

    [Fact]
    public void No_prediction_at_100()
    {
        var p = new UsagePredictor();
        p.Add(T0, U(80));
        p.Add(T0.AddMinutes(20), U(100));

        Assert.Null(p.PredictFull());
    }
}
