using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsageWidget.Core;

/// <summary>When the widget last called the usage endpoint, so a restart doesn't fetch too soon after it.</summary>
public sealed record FetchState(DateTimeOffset LastFetchAt, FetchStatus LastStatus, DateTimeOffset NextFetchAt);

public sealed class FetchStateStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeUsageWidget", "state.json");

    /// <summary>Returns null when the file is missing, locked or corrupt.</summary>
    public FetchState? Load()
    {
        try
        {
            return JsonSerializer.Deserialize<FetchState>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Best effort: losing this only means a restart may fetch a little early.</summary>
    public void Save(FetchState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
