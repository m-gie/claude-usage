using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}");
    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_missing_returns_defaults()
    {
        var s = new SettingsStore(SettingsPath).Load();

        Assert.Null(s.Left);
        Assert.Null(s.Top);
        Assert.True(s.WidgetVisible);
        Assert.Equal("Ctrl+Alt+U", s.Hotkey);
        Assert.Equal(60, s.PollSeconds);
        Assert.False(s.StartWithWindows);
        Assert.False(s.PositionUnlocked);
    }

    [Fact]
    public void Roundtrip()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(new AppSettings
        {
            Left = 100.5, Top = 20, WidgetVisible = false, Hotkey = "Ctrl+Shift+F9",
            PollSeconds = 30, StartWithWindows = true, PositionUnlocked = true,
        });

        var s = store.Load();

        Assert.Equal(100.5, s.Left);
        Assert.Equal(20, s.Top);
        Assert.False(s.WidgetVisible);
        Assert.Equal("Ctrl+Shift+F9", s.Hotkey);
        Assert.Equal(30, s.PollSeconds);
        Assert.True(s.StartWithWindows);
        Assert.True(s.PositionUnlocked);
    }

    [Fact]
    public void Saved_file_is_hand_editable()
    {
        new SettingsStore(SettingsPath).Save(new AppSettings());

        Assert.Contains("\"Hotkey\": \"Ctrl+Alt+U\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Corrupt_file_returns_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{not json");

        var s = new SettingsStore(SettingsPath).Load();

        Assert.Equal(60, s.PollSeconds);
        Assert.Equal("Ctrl+Alt+U", s.Hotkey);
    }

    [Fact]
    public void Poll_clamped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{"PollSeconds":0}""");

        Assert.Equal(15, new SettingsStore(SettingsPath).Load().PollSeconds);
    }
}
