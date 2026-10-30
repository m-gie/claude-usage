using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public class PollScheduleTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData(60, 120)]
    [InlineData(480, 600)]
    [InlineData(600, 600)]
    public void RateLimited_doubles_up_to_ten_minutes(int currentSeconds, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds),
            PollSchedule.Next(Base, TimeSpan.FromSeconds(currentSeconds), FetchStatus.RateLimited));
    }

    [Theory]
    [InlineData(FetchStatus.Ok)]
    [InlineData(FetchStatus.Error)]
    [InlineData(FetchStatus.TokenExpired)]
    [InlineData(FetchStatus.NotLoggedIn)]
    public void Other_statuses_reset_to_base(FetchStatus status)
    {
        Assert.Equal(Base, PollSchedule.Next(Base, TimeSpan.FromSeconds(480), status));
    }
}
