namespace ClaudeUsageWidget.Core;

/// <summary>Estimates when the session limit will be hit from the recent pace within the current window.</summary>
public sealed class UsagePredictor
{
    private static readonly TimeSpan Lookback = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinElapsed = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan WindowTolerance = TimeSpan.FromMinutes(5);
    private const double MinDelta = 2;

    private readonly List<(DateTimeOffset At, double Percent)> _samples = [];
    private DateTimeOffset? _resetsAt;

    public void Add(DateTimeOffset at, SessionUsage usage)
    {
        if (_samples.Count > 0 && (IsNewWindow(usage.ResetsAt) || usage.Percent < _samples[^1].Percent))
            _samples.Clear();

        _resetsAt = usage.ResetsAt;
        _samples.Add((at, usage.Percent));
        _samples.RemoveAll(s => at - s.At > Lookback);
    }

    public DateTimeOffset? PredictFull()
    {
        if (_samples.Count < 2 || _resetsAt is null)
            return null;

        var (firstAt, firstPercent) = _samples[0];
        var (lastAt, lastPercent) = _samples[^1];
        var elapsed = lastAt - firstAt;
        var delta = lastPercent - firstPercent;
        if (lastPercent >= 100 || delta < MinDelta || elapsed < MinElapsed)
            return null;

        var perMinute = delta / elapsed.TotalMinutes;
        var projected = lastAt + TimeSpan.FromMinutes((100 - lastPercent) / perMinute);
        return projected < _resetsAt ? projected : null;
    }

    private bool IsNewWindow(DateTimeOffset? resetsAt) =>
        (resetsAt is null) != (_resetsAt is null)
        || (resetsAt is { } a && _resetsAt is { } b && (a - b).Duration() > WindowTolerance);
}
