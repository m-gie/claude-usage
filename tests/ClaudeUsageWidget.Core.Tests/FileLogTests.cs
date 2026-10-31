using ClaudeUsageWidget.Core;
using Microsoft.Extensions.Time.Testing;

namespace ClaudeUsageWidget.Core.Tests;

public sealed class FileLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"log-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 31, 9, 42, 15, TimeSpan.Zero));
    private string LogPath => Path.Combine(_dir, "widget.log");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Writes_timestamped_lines_creating_the_folder()
    {
        var log = new FileLog(LogPath, maxBytes: 1024, _time, TimeZoneInfo.Utc);

        log.Write("first");
        _time.Advance(TimeSpan.FromSeconds(3));
        log.Write("second");

        Assert.Equal(["2026-10-31 09:42:15 first", "2026-10-31 09:42:18 second"], File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Rotates_to_dot_1_when_full()
    {
        var log = new FileLog(LogPath, maxBytes: 60, _time, TimeZoneInfo.Utc);

        log.Write("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        log.Write("bbb");

        Assert.Equal(["2026-10-31 09:42:15 bbb"], File.ReadAllLines(LogPath));
        Assert.EndsWith(" aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", File.ReadAllText(LogPath + ".1").TrimEnd());
    }

    [Fact]
    public void Waits_briefly_for_another_process_writing_the_same_file()
    {
        Directory.CreateDirectory(_dir);
        var other = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _ = Task.Delay(50).ContinueWith(_ => other.Dispose());

        new FileLog(LogPath, maxBytes: 1024, _time, TimeZoneInfo.Utc).Write("not lost");

        Assert.Equal(["2026-10-31 09:42:15 not lost"], File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Never_throws_when_the_file_cannot_be_written()
    {
        Directory.CreateDirectory(_dir);
        using var locked = new FileStream(LogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        new FileLog(LogPath, maxBytes: 1024, _time, TimeZoneInfo.Utc).Write("dropped");
    }
}
