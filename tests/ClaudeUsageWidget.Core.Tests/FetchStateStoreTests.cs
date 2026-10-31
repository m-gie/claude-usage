using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public sealed class FetchStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"state-{Guid.NewGuid():N}");
    private string StatePath => Path.Combine(_dir, "state.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_is_null()
    {
        Assert.Null(new FetchStateStore(StatePath).Load());
    }

    [Fact]
    public void Roundtrip_creating_the_folder()
    {
        var state = new FetchState(new DateTimeOffset(2026, 10, 31, 12, 10, 36, TimeSpan.FromHours(1)), FetchStatus.RateLimited,
            new DateTimeOffset(2026, 10, 31, 12, 14, 36, TimeSpan.FromHours(1)));
        var store = new FetchStateStore(StatePath);

        store.Save(state);

        Assert.Equal(state, store.Load());
    }

    [Fact]
    public void Corrupt_file_is_null()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(StatePath, "{not json");

        Assert.Null(new FetchStateStore(StatePath).Load());
    }

    [Fact]
    public void Save_never_throws_when_the_file_is_locked()
    {
        Directory.CreateDirectory(_dir);
        using var locked = new FileStream(StatePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        new FetchStateStore(StatePath).Save(new FetchState(DateTimeOffset.UnixEpoch, FetchStatus.Ok, DateTimeOffset.UnixEpoch));
    }
}
