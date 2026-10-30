using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget;

/// <summary>Polls the usage endpoint and publishes display state on the thread that called Start.</summary>
public sealed class UsageMonitor(UsageClient client, TimeSpan baseInterval, TimeProvider time) : IDisposable
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

    public event Action<DisplayState>? StateChanged;

    public DisplayState Current { get; private set; } =
        DisplayStateBuilder.Build(null, null, null, null, time.GetUtcNow(), TimeZoneInfo.Local);

    public void Start()
    {
        _sync = SynchronizationContext.Current;
        _tick = time.CreateTimer(_ => Post(Publish), null, TickInterval, TickInterval);
        _ = RunAsync(_stop.Token);
    }

    /// <summary>After sleep the timers lag behind wall-clock time, so redraw now and fetch fresh data.</summary>
    public void OnResume()
    {
        Post(Publish);
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (_wake.CurrentCount == 0)
            _wake.Release();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var interval = baseInterval;
        while (!ct.IsCancellationRequested)
        {
            FetchResult result;
            try
            {
                result = await client.FetchAsync(ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Never let one unexpected failure end polling; show it as an error and keep going.
                result = new FetchResult(FetchStatus.Error, null);
            }
            Post(() => Record(result));
            interval = PollSchedule.Next(baseInterval, interval, result.Status);

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

    private void Record(FetchResult result)
    {
        _status = result.Status;
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
        Current = DisplayStateBuilder.Build(_status, _lastGood, _lastGoodAt, _predictor.PredictFull(), time.GetUtcNow(), TimeZoneInfo.Local);
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
