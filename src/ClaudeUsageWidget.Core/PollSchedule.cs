namespace ClaudeUsageWidget.Core;

public static class PollSchedule
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromHours(1);

    /// <summary>
    /// While rate limited or the server errors, doubles the wait (up to 10 min), or waits longer if the
    /// server's Retry-After asks for it (up to 1 h). Any other result goes back to the base interval.
    /// </summary>
    public static TimeSpan Next(TimeSpan baseInterval, TimeSpan current, FetchStatus status, TimeSpan? retryAfter)
    {
        if (!IsBackoff(status))
            return baseInterval;

        var doubled = TimeSpan.FromTicks(Math.Min(current.Ticks * 2, Math.Max(MaxBackoff.Ticks, baseInterval.Ticks)));
        var asked = retryAfter is { } r ? TimeSpan.FromTicks(Math.Min(r.Ticks, MaxRetryAfter.Ticks)) : TimeSpan.Zero;
        return asked > doubled ? asked : doubled;
    }

    /// <summary>The endpoint rate-limits requests even seconds apart, so on-demand fetches need a gap from the last one.</summary>
    public static readonly TimeSpan MinManualGap = TimeSpan.FromMinutes(1);

    /// <summary>
    /// On-demand fetches (refresh, show, resume) wait at least a minute after the last fetch, and for the
    /// whole backoff after a rate limit or server error, since an early request only prolongs the limit.
    /// </summary>
    public static bool AllowsManualRefresh(FetchStatus? last, DateTimeOffset? lastFetchAt, DateTimeOffset nextFetchAt, DateTimeOffset now)
    {
        if (lastFetchAt is { } at && now - at < MinManualGap)
            return false;
        return last is not { } status || !IsBackoff(status) || now >= nextFetchAt;
    }

    private static bool IsBackoff(FetchStatus status) => status is FetchStatus.RateLimited or FetchStatus.ServerError;
}
