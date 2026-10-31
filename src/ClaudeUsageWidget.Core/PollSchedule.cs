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

    /// <summary>The endpoint rate-limits requests less than about two minutes apart, so on-demand fetches wait that long.</summary>
    public static readonly TimeSpan MinManualGap = TimeSpan.FromMinutes(2);

    /// <summary>
    /// On-demand fetches (refresh, show, resume) wait at least two minutes after the last fetch, and for the
    /// whole backoff after a rate limit or server error, since an early request only prolongs the limit.
    /// </summary>
    public static bool AllowsManualRefresh(FetchStatus? last, DateTimeOffset? lastFetchAt, DateTimeOffset nextFetchAt, DateTimeOffset now)
    {
        if (lastFetchAt is { } at && now - at < MinManualGap)
            return false;
        return last is not { } status || !IsBackoff(status) || now >= nextFetchAt;
    }

    /// <summary>
    /// How long a freshly started widget waits before its first fetch: the rest of the gap after the last
    /// fetch, or of a backoff still running from before the restart, capped at one hour.
    /// </summary>
    public static TimeSpan InitialDelay(FetchState? saved, DateTimeOffset now)
    {
        if (saved is null)
            return TimeSpan.Zero;

        var gap = saved.LastFetchAt + MinManualGap - now;
        var backoff = IsBackoff(saved.LastStatus) ? saved.NextFetchAt - now : TimeSpan.Zero;
        var wait = gap > backoff ? gap : backoff;
        return wait <= TimeSpan.Zero ? TimeSpan.Zero : wait > MaxRetryAfter ? MaxRetryAfter : wait;
    }

    public static bool IsBackoff(FetchStatus status) => status is FetchStatus.RateLimited or FetchStatus.ServerError;
}
