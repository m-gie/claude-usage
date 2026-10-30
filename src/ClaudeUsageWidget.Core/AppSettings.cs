using System.Text.Encodings.Web;
using System.Text.Json;

namespace ClaudeUsageWidget.Core;

public sealed class AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+U";
    public const int MinPollSeconds = 15;
    public const int MaxPollSeconds = 3600;

    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool WidgetVisible { get; set; } = true;
    public string Hotkey { get; set; } = DefaultHotkey;
    public int PollSeconds { get; set; } = 60;
    public bool StartWithWindows { get; set; }
    public bool PositionUnlocked { get; set; }
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Keep "+" readable in hotkeys; the file is meant to be edited by hand.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeUsageWidget", "settings.json");

    /// <summary>Returns defaults when the file is missing or corrupt.</summary>
    public AppSettings Load()
    {
        AppSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            settings = new AppSettings();
        }

        settings.PollSeconds = Math.Clamp(settings.PollSeconds, AppSettings.MinPollSeconds, AppSettings.MaxPollSeconds);
        settings.Hotkey ??= AppSettings.DefaultHotkey;
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }
}
