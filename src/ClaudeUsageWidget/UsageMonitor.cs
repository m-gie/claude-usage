using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget;

/// <summary>Polls the usage endpoint and publishes display state on the thread that called Start.</summary>
public sealed class UsageMonitor(UsageClient client, TimeSpan baseInterval, TimeProvider time, FileLog log, FetchStateStore state)
    : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly UsagePredictor _predictor = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly CancellationTokenSource _stop = new();
    private SynchronizationContext? _sync;
    private ITimer? _tick;

    private FetchStatus? _status;
    private SessionUsage? _lastGood;
    private DateTimeOffset? _lastGoodAt;
    private DateTimeOffset _nextFetchAt;
    private DateTimeOffset? _lastFetchAt;

    public event Action<DisplayState>? StateChanged;

    /// <summary>Show the estimate even when the limit lands after the reset. Call on the Start thread.</summary>
    public bool AlwaysShowEstimate
    {
        get;
        set
        {
            field = value;
            Publish();
        }
    }

    public DisplayState Current { get; private set; } =
        DisplayStateBuilder.Build(null, null, null, null, time.GetUtcNow(), TimeZoneInfo.Local);

    public void Start()
    {
        _sync = SynchronizationContext.Current;

        // Pick up where the last run left off, so restarting the app can't hit the endpoint too soon.
        var saved = state.Load();
        var interval = baseInterval;
        if (saved is not null)
        {
            _lastFetchAt = saved.LastFetchAt;
            _nextFetchAt = saved.NextFetchAt;
            if (PollSchedule.IsBackoff(saved.LastStatus))
            {
                _status = saved.LastStatus;
                var savedInterval = saved.NextFetchAt - saved.LastFetchAt;
                interval = savedInterval > baseInterval ? savedInterval : baseInterval;
                Publish();
            }
        }

        _tick = time.CreateTimer(_ => Post(Publish), null, TickInterval, TickInterval);
        _ = RunAsync(PollSchedule.InitialDelay(saved, time.GetUtcNow()), interval, saved, _stop.Token);
    }

    /// <summary>After sleep the timers lag behind wall-clock time, so redraw now and fetch fresh data.</summary>
    public void OnResume()
    {
        Post(Publish);
        RefreshNow("resume");
    }

    /// <summary>
    /// Call on the thread that called Start. Skipped within two minutes of the last fetch and during a backoff,
    /// so on-demand refreshes can't trigger or prolong a rate limit.
    /// </summary>
    public void RefreshNow(string reason)
    {
        if (!PollSchedule.AllowsManualRefresh(_status, _lastFetchAt, _nextFetchAt, time.GetUtcNow()))
        {
            log.Write($"refresh ({reason}) skipped: last fetch {_lastFetchAt?.ToLocalTime():HH:mm:ss} was {_status}, "
                      + $"next at {_nextFetchAt.ToLocalTime():HH:mm:ss}");
            return;
        }
        log.Write($"refresh ({reason})");
        if (_wake.CurrentCount == 0)
            _wake.Release();
    }

    private async Task RunAsync(TimeSpan initialDelay, TimeSpan interval, FetchState? saved, CancellationToken ct)
    {
        if (initialDelay > TimeSpan.Zero)
        {
            log.Write($"first fetch in {initialDelay.TotalSeconds:0}s: last fetch {saved!.LastFetchAt.ToLocalTime():HH:mm:ss} "
                      + $"was {saved.LastStatus}");
            try
            {
                await _wake.WaitAsync(initialDelay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        while (!ct.IsCancellationRequested)
        {
            FetchResult result;
            // Runs on the Start thread, like RefreshNow, so an in-flight fetch already counts as recent.
            var fetchedAt = time.GetUtcNow();
            _lastFetchAt = fetchedAt;
            try
            {
                result = await client.FetchAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Never let one unexpected failure end polling; show it as an error and keep going.
                result = new FetchResult(FetchStatus.Error, null, Detail: $"unexpected {ex}");
            }
            interval = PollSchedule.Next(baseInterval, interval, result.Status, result.RetryAfter);
            var nextFetchAt = time.GetUtcNow() + interval;
            log.Write(Describe(result, interval));
            state.Save(new FetchState(fetchedAt, result.Status, nextFetchAt));
            Post(() => Record(result, nextFetchAt));

            try
            {
                await _wake.WaitAsync(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static string Describe(FetchResult result, TimeSpan next)
    {
        var parts = new List<string> { $"fetch {result.Status}" };
        if (result.HttpStatus is { } code)
            parts.Add($"http={code}");
        if (result.Usage is { } usage)
            parts.Add($"usage={usage.Percent}%");
        if (result.RetryAfter is { } retryAfter)
            parts.Add($"retry-after={retryAfter.TotalSeconds:0}s");
        parts.Add($"next in {next.TotalSeconds:0}s");
        if (result.Detail is { } detail)
            parts.Add($"| {detail}");
        return string.Join(' ', parts);
    }

    private void Record(FetchResult result, DateTimeOffset nextFetchAt)
    {
        _status = result.Status;
        _nextFetchAt = nextFetchAt;
        if (result is { Status: FetchStatus.Ok, Usage: { } usage })
        {
            var now = time.GetUtcNow();
            _lastGood = usage;
            _lastGoodAt = now;
            _predictor.Add(now, usage);
        }
        Publish();
    }

    private void Publish()
    {
        Current = DisplayStateBuilder.Build(_status, _lastGood, _lastGoodAt, _predictor.PredictFull(), time.GetUtcNow(),
            TimeZoneInfo.Local, AlwaysShowEstimate);
        StateChanged?.Invoke(Current);
    }

    private void Post(Action action)
    {
        if (_sync is null)
            action();
        else
            _sync.Post(_ => action(), null);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _tick?.Dispose();
    }
}
