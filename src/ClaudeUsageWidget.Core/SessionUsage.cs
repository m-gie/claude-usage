using System.Text.Json;

namespace ClaudeUsageWidget.Core;

/// <summary>5-hour session usage: percent used (0-100) and when the window resets.</summary>
public sealed record SessionUsage(double Percent, DateTimeOffset? ResetsAt);

public sealed class UsageParseException(string message, Exception? inner = null) : Exception(message, inner);

public static class UsageParser
{
    public static SessionUsage Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("five_hour", out var fiveHour))
            {
                throw new UsageParseException("Response has no five_hour field.");
            }

            if (fiveHour.ValueKind == JsonValueKind.Null)
                return new SessionUsage(0, null);

            var percent = fiveHour.GetProperty("utilization").GetDouble();
            DateTimeOffset? resetsAt = null;
            if (fiveHour.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.String)
                resetsAt = reset.GetDateTimeOffset();

            return new SessionUsage(percent, resetsAt);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new UsageParseException("Unexpected usage response format.", ex);
        }
    }
}
