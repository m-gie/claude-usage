namespace ClaudeUsageWidget.Core;

public static class PollSchedule
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    /// <summary>Doubles the wait (up to 10 min) while rate limited or the server errors; otherwise back to base.</summary>
    public static TimeSpan Next(TimeSpan baseInterval, TimeSpan current, FetchStatus status) =>
        status == FetchStatus.RateLimited
            ? TimeSpan.FromTicks(Math.Min(current.Ticks * 2, Math.Max(MaxBackoff.Ticks, baseInterval.Ticks)))
            : baseInterval;
}
