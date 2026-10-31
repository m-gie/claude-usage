using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public class DisplayStateBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 14, 48, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Reset = new(2026, 10, 30, 16, 40, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastGoodAt = new(2026, 10, 30, 14, 20, 0, TimeSpan.Zero);

    private static DisplayState Build(FetchStatus? status, SessionUsage? usage, DateTimeOffset? predicted = null,
        DateTimeOffset? lastGoodAt = null, DateTimeOffset? now = null, bool alwaysShowEstimate = false) =>
        DisplayStateBuilder.Build(status, usage, lastGoodAt ?? (usage is null ? null : LastGoodAt), predicted, now ?? Now, TimeZoneInfo.Utc,
            alwaysShowEstimate);

    [Fact]
    public void Ok_state()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset));

        Assert.Equal(62, s.Percent);
        Assert.Equal(Level.Normal, s.Level);
        Assert.Equal("Resets 16:40", s.ResetLine);
        Assert.Equal("in 1h 52m", s.CountdownLine);
        Assert.Null(s.DetailLine);
        Assert.Equal("Claude: 62% · resets 16:40", s.Tooltip);
    }

    [Fact]
    public void Ok_with_prediction()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: new DateTimeOffset(2026, 10, 30, 15, 55, 0, TimeSpan.Zero));

        Assert.Equal("full ~15:55", s.DetailLine);
        Assert.Equal(DetailTone.Warning, s.DetailTone);
        Assert.Equal("Claude: 62% · resets 16:40 · full ~15:55", s.Tooltip);
    }

    [Fact]
    public void Past_prediction_is_hidden()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: Now.AddMinutes(-1));

        Assert.Null(s.DetailLine);
        Assert.Equal("Claude: 62% · resets 16:40", s.Tooltip);
    }

    [Fact]
    public void Estimate_after_reset_is_hidden_by_default()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: Reset.AddMinutes(35));

        Assert.Null(s.DetailLine);
        Assert.Equal("Claude: 62% · resets 16:40", s.Tooltip);
    }

    [Fact]
    public void Always_show_estimate_after_reset_is_good_news()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: Reset.AddMinutes(35), alwaysShowEstimate: true);

        Assert.Equal("full ~17:15", s.DetailLine);
        Assert.Equal(DetailTone.Good, s.DetailTone);
        Assert.Equal("Claude: 62% · resets 16:40 · full ~17:15", s.Tooltip);
    }

    [Fact]
    public void Always_show_estimate_before_reset_is_still_a_warning()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: Reset.AddMinutes(-20), alwaysShowEstimate: true);

        Assert.Equal("full ~16:20", s.DetailLine);
        Assert.Equal(DetailTone.Warning, s.DetailTone);
    }

    [Fact]
    public void Always_show_estimate_without_a_pace_is_estimating()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: null, alwaysShowEstimate: true);

        Assert.Equal("estimating…", s.DetailLine);
        Assert.Equal(DetailTone.Dim, s.DetailTone);
        Assert.Equal("Claude: 62% · resets 16:40", s.Tooltip);
    }

    [Fact]
    public void Always_show_estimate_more_than_a_day_out()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(62, Reset), predicted: Now.AddHours(25), alwaysShowEstimate: true);

        Assert.Equal("full >24h", s.DetailLine);
        Assert.Equal(DetailTone.Good, s.DetailTone);
    }

    [Fact]
    public void Always_show_estimate_does_not_hide_limit_reached()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(100, Reset), predicted: null, alwaysShowEstimate: true);

        Assert.Equal("Limit reached", s.DetailLine);
        Assert.Equal(DetailTone.Warning, s.DetailTone);
    }

    [Fact]
    public void Always_show_estimate_does_not_hide_errors()
    {
        var s = Build(FetchStatus.RateLimited, new SessionUsage(62, Reset), predicted: Reset.AddMinutes(35), alwaysShowEstimate: true);

        Assert.Equal("Rate limited · updated 14:20", s.DetailLine);
    }

    [Theory]
    [InlineData(0, Level.Normal)]
    [InlineData(69.9, Level.Normal)]
    [InlineData(70, Level.Warning)]
    [InlineData(89, Level.Warning)]
    [InlineData(90, Level.Critical)]
    public void Levels(double percent, Level expected)
    {
        Assert.Equal(expected, Build(FetchStatus.Ok, new SessionUsage(percent, Reset)).Level);
    }

    [Fact]
    public void At_100_limit_reached()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(100, Reset));

        Assert.Equal("Limit reached", s.DetailLine);
        Assert.Equal(Level.Critical, s.Level);
    }

    [Fact]
    public void Expired_is_stale()
    {
        var s = Build(FetchStatus.TokenExpired, new SessionUsage(62, Reset));

        Assert.Equal(Level.Inactive, s.Level);
        Assert.Equal(62, s.Percent);
        Assert.Equal("Open Claude Code to refresh", s.DetailLine);
        Assert.Equal(DetailTone.Dim, s.DetailTone);
        Assert.Equal("Claude: Open Claude Code to refresh (last 62%)", s.Tooltip);
    }

    [Fact]
    public void NotLoggedIn_without_data()
    {
        var s = Build(FetchStatus.NotLoggedIn, null);

        Assert.Null(s.Percent);
        Assert.Equal(Level.Inactive, s.Level);
        Assert.Equal("Not logged in to Claude Code", s.DetailLine);
        Assert.Equal("", s.ResetLine);
        Assert.Equal("", s.CountdownLine);
        Assert.Equal("Claude: Not logged in to Claude Code", s.Tooltip);
    }

    [Theory]
    [InlineData(FetchStatus.Error, "Can't reach Claude · updated 14:20")]
    [InlineData(FetchStatus.RateLimited, "Rate limited · updated 14:20")]
    [InlineData(FetchStatus.ServerError, "Server error · updated 14:20")]
    public void Stale_data_says_why_and_when(FetchStatus status, string expected)
    {
        var s = Build(status, new SessionUsage(62, Reset));

        Assert.Equal(Level.Inactive, s.Level);
        Assert.Equal(expected, s.DetailLine);
        Assert.Equal($"Claude: {expected} (last 62%)", s.Tooltip);
    }

    [Theory]
    [InlineData(FetchStatus.Error, "Can't reach Claude")]
    [InlineData(FetchStatus.RateLimited, "Rate limited")]
    [InlineData(FetchStatus.ServerError, "Server error")]
    public void Failure_without_data_says_why(FetchStatus status, string expected)
    {
        Assert.Equal(expected, Build(status, null).DetailLine);
    }

    [Fact]
    public void Loading()
    {
        var s = Build(null, null);

        Assert.Null(s.Percent);
        Assert.Equal("Loading…", s.DetailLine);
        Assert.Equal("Claude: Loading…", s.Tooltip);
    }

    [Fact]
    public void No_reset_time()
    {
        var s = Build(FetchStatus.Ok, new SessionUsage(0, null));

        Assert.Equal("No active session", s.ResetLine);
        Assert.Equal("", s.CountdownLine);
        Assert.Equal("Claude: 0% · no active session", s.Tooltip);
    }

    [Fact]
    public void Reset_passed_while_stale()
    {
        var s = Build(FetchStatus.Error, new SessionUsage(62, new DateTimeOffset(2026, 10, 30, 14, 0, 0, TimeSpan.Zero)));

        Assert.Equal("Resetting…", s.CountdownLine);
    }

    [Theory]
    [InlineData(15, 40, 0, "in 52m")]
    [InlineData(14, 48, 30, "in <1m")]
    [InlineData(16, 40, 59, "in 1h 52m")]
    public void Countdown(int h, int m, int sec, string expected)
    {
        var reset = new DateTimeOffset(2026, 10, 30, h, m, sec, TimeSpan.Zero);

        Assert.Equal(expected, Build(FetchStatus.Ok, new SessionUsage(10, reset)).CountdownLine);
    }

    [Fact]
    public void Times_use_local_zone()
    {
        var warsaw = TimeZoneInfo.CreateCustomTimeZone("Test+1", TimeSpan.FromHours(1), "Test+1", "Test+1");

        var s = DisplayStateBuilder.Build(FetchStatus.Ok, new SessionUsage(10, Reset), LastGoodAt, null, Now, warsaw);

        Assert.Equal("Resets 17:40", s.ResetLine);
    }
}
