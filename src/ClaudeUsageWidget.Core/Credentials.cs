using System.Text.Json;

namespace ClaudeUsageWidget.Core;

public sealed record Credentials(string AccessToken, DateTimeOffset ExpiresAt)
{
    // Keep the token out of logs and debugger displays.
    public override string ToString() => $"Credentials {{ ExpiresAt = {ExpiresAt:O} }}";
}

/// <summary>Reads Claude Code's OAuth token. Read-only: never writes or refreshes it.</summary>
public static class CredentialsReader
{
    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    /// <summary>Returns null when the file is missing, locked, partially written or has no Claude login.</summary>
    public static Credentials? TryRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                || !oauth.TryGetProperty("accessToken", out var token)
                || token.ValueKind != JsonValueKind.String
                || !oauth.TryGetProperty("expiresAt", out var expires)
                || !expires.TryGetInt64(out var expiresMs))
            {
                return null;
            }

            return new Credentials(token.GetString()!, DateTimeOffset.FromUnixTimeMilliseconds(expiresMs));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
