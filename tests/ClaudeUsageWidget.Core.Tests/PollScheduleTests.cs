using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public class PollScheduleTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(180);
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(FetchStatus.RateLimited, 180, 360)]
    [InlineData(FetchStatus.RateLimited, 480, 600)]
    [InlineData(FetchStatus.RateLimited, 600, 600)]
    [InlineData(FetchStatus.ServerError, 180, 360)]
    [InlineData(FetchStatus.ServerError, 600, 600)]
    public void Backoff_doubles_up_to_ten_minutes(FetchStatus status, int currentSeconds, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds),
            PollSchedule.Next(Base, TimeSpan.FromSeconds(currentSeconds), status, retryAfter: null));
    }

    [Fact]
    public void Retry_after_longer_than_backoff_wins()
    {
        Assert.Equal(TimeSpan.FromMinutes(20),
            PollSchedule.Next(Base, Base, FetchStatus.RateLimited, TimeSpan.FromMinutes(20)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void Retry_after_shorter_than_backoff_is_ignored(int retryAfterSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(360),
            PollSchedule.Next(Base, Base, FetchStatus.RateLimited, TimeSpan.FromSeconds(retryAfterSeconds)));
    }

    [Fact]
    public void Retry_after_is_capped_at_one_hour()
    {
        Assert.Equal(TimeSpan.FromHours(1),
            PollSchedule.Next(Base, Base, FetchStatus.RateLimited, TimeSpan.FromHours(5)));
    }

    [Theory]
    [InlineData(FetchStatus.Ok)]
    [InlineData(FetchStatus.Error)]
    [InlineData(FetchStatus.TokenExpired)]
    [InlineData(FetchStatus.NotLoggedIn)]
    public void Other_statuses_reset_to_base(FetchStatus status)
    {
        Assert.Equal(Base, PollSchedule.Next(Base, TimeSpan.FromSeconds(480), status, TimeSpan.FromMinutes(20)));
    }

    private static readonly DateTimeOffset LongAgo = Now.AddMinutes(-5);

    [Theory]
    [InlineData(FetchStatus.RateLimited)]
    [InlineData(FetchStatus.ServerError)]
    public void Manual_refresh_is_blocked_during_backoff(FetchStatus status)
    {
        Assert.False(PollSchedule.AllowsManualRefresh(status, LongAgo, nextFetchAt: Now.AddMinutes(5), Now));
    }

    [Theory]
    [InlineData(FetchStatus.RateLimited)]
    [InlineData(FetchStatus.ServerError)]
    public void Manual_refresh_is_allowed_once_backoff_has_passed(FetchStatus status)
    {
        Assert.True(PollSchedule.AllowsManualRefresh(status, LongAgo, nextFetchAt: Now, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FetchStatus.Ok)]
    [InlineData(FetchStatus.Error)]
    [InlineData(FetchStatus.TokenExpired)]
    [InlineData(FetchStatus.NotLoggedIn)]
    public void Manual_refresh_is_allowed_outside_backoff(FetchStatus? status)
    {
        Assert.True(PollSchedule.AllowsManualRefresh(status, LongAgo, nextFetchAt: Now.AddMinutes(5), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(59)]
    public void Manual_refresh_is_blocked_within_a_minute_of_the_last_fetch(int secondsAgo)
    {
        Assert.False(PollSchedule.AllowsManualRefresh(FetchStatus.Ok, Now.AddSeconds(-secondsAgo), Now.AddMinutes(2), Now));
    }

    [Fact]
    public void Manual_refresh_is_allowed_a_minute_after_the_last_fetch()
    {
        Assert.True(PollSchedule.AllowsManualRefresh(FetchStatus.Ok, Now.AddSeconds(-60), Now.AddMinutes(1), Now));
    }

    [Fact]
    public void Manual_refresh_is_allowed_before_any_fetch()
    {
        Assert.True(PollSchedule.AllowsManualRefresh(null, lastFetchAt: null, Now.AddMinutes(2), Now));
    }
}
