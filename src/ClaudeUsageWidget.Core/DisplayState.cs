using System.Globalization;

namespace ClaudeUsageWidget.Core;

public enum Level { Normal, Warning, Critical, Inactive }

/// <summary>Everything the widget and tray need to render, already formatted.</summary>
public sealed record DisplayState(
    double? Percent,
    Level Level,
    string ResetLine,
    string CountdownLine,
    string? DetailLine,
    bool DetailIsWarning,
    string Tooltip);

public static class DisplayStateBuilder
{
    public static DisplayState Build(FetchStatus? status, SessionUsage? lastGood, DateTimeOffset? lastGoodAt,
        DateTimeOffset? predictedFull, DateTimeOffset now, TimeZoneInfo tz)
    {
        string Time(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, tz).ToString("HH:mm", CultureInfo.InvariantCulture);

        var resetLine = "";
        var countdownLine = "";
        if (lastGood is not null)
        {
            if (lastGood.ResetsAt is { } reset)
            {
                resetLine = $"Resets {Time(reset)}";
                countdownLine = Countdown(reset - now);
            }
            else
            {
                resetLine = "No active session";
            }
        }

        if (status == FetchStatus.Ok && lastGood is not null)
        {
            var percent = lastGood.Percent;
            string? detail = percent >= 100 ? "Limit reached"
                : predictedFull is { } full && full > now ? $"full ~{Time(full)}"
                : null;
            var tooltip = $"Claude: {FormatPercent(percent)} · "
                          + (lastGood.ResetsAt is { } r ? $"resets {Time(r)}" : "no active session")
                          + (detail is null ? "" : $" · {detail}");
            return new DisplayState(percent, LevelFor(percent), resetLine, countdownLine, detail, detail is not null, tooltip);
        }

        var staleDetail = status switch
        {
            null => "Loading…",
            FetchStatus.TokenExpired => "Open Claude Code to refresh",
            FetchStatus.NotLoggedIn => "Not logged in to Claude Code",
            _ => lastGoodAt is { } at ? $"Updated {Time(at)}" : "Can't reach Claude",
        };
        var staleTooltip = $"Claude: {staleDetail}" + (lastGood is null ? "" : $" (last {FormatPercent(lastGood.Percent)})");
        return new DisplayState(lastGood?.Percent, Level.Inactive, resetLine, countdownLine, staleDetail, false, staleTooltip);
    }

    private static Level LevelFor(double percent) => percent switch
    {
        >= 90 => Level.Critical,
        >= 70 => Level.Warning,
        _ => Level.Normal,
    };

    private static string FormatPercent(double percent) => $"{Math.Round(percent).ToString(CultureInfo.InvariantCulture)}%";

    private static string Countdown(TimeSpan left)
    {
        if (left <= TimeSpan.Zero)
            return "Resetting…";
        if (left < TimeSpan.FromMinutes(1))
            return "in <1m";
        var hours = (int)left.TotalHours;
        return hours > 0 ? $"in {hours}h {left.Minutes}m" : $"in {left.Minutes}m";
    }
}
