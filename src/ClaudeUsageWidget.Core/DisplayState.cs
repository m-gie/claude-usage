using System.Globalization;

namespace ClaudeUsageWidget.Core;

public enum Level { Normal, Warning, Critical, Inactive }

/// <summary>How the detail line reads: neutral, a warning (limit before reset), or good news (limit after reset).</summary>
public enum DetailTone { Dim, Warning, Good }

/// <summary>Everything the widget and tray need to render, already formatted.</summary>
public sealed record DisplayState(
    double? Percent,
    Level Level,
    string ResetLine,
    string CountdownLine,
    string? DetailLine,
    DetailTone DetailTone,
    string Tooltip);

public static class DisplayStateBuilder
{
    private const string Estimating = "estimating…";
    private static readonly TimeSpan FarAhead = TimeSpan.FromHours(24);

    /// <param name="predictedFull">Projected limit hit at the recent pace, whether before or after the reset.</param>
    /// <param name="alwaysShowEstimate">Also show a projection past the reset (in green), or "estimating…" without one.</param>
    public static DisplayState Build(FetchStatus? status, SessionUsage? lastGood, DateTimeOffset? lastGoodAt,
        DateTimeOffset? predictedFull, DateTimeOffset now, TimeZoneInfo tz, bool alwaysShowEstimate = false)
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
            var (detail, tone) = percent >= 100
                ? ("Limit reached", DetailTone.Warning)
                : Estimate(predictedFull, lastGood.ResetsAt, now, alwaysShowEstimate, Time);
            var tooltip = $"Claude: {FormatPercent(percent)} · "
                          + (lastGood.ResetsAt is { } r ? $"resets {Time(r)}" : "no active session")
                          + (detail is null or Estimating ? "" : $" · {detail}");
            return new DisplayState(percent, LevelFor(percent), resetLine, countdownLine, detail, tone, tooltip);
        }

        var staleDetail = status switch
        {
            null => "Loading…",
            FetchStatus.TokenExpired => "Open Claude Code to refresh",
            FetchStatus.NotLoggedIn => "Not logged in to Claude Code",
            _ => FailureReason(status.Value) + (lastGoodAt is { } at ? $" · updated {Time(at)}" : ""),
        };
        var staleTooltip = $"Claude: {staleDetail}" + (lastGood is null ? "" : $" (last {FormatPercent(lastGood.Percent)})");
        return new DisplayState(lastGood?.Percent, Level.Inactive, resetLine, countdownLine, staleDetail, DetailTone.Dim, staleTooltip);
    }

    private static (string? Detail, DetailTone Tone) Estimate(DateTimeOffset? predictedFull, DateTimeOffset? resetsAt,
        DateTimeOffset now, bool alwaysShow, Func<DateTimeOffset, string> time)
    {
        if (resetsAt is not { } reset)
            return (null, DetailTone.Dim);
        if (predictedFull is not { } full || full <= now)
            return alwaysShow ? (Estimating, DetailTone.Dim) : (null, DetailTone.Dim);
        if (full < reset)
            return ($"full ~{time(full)}", DetailTone.Warning);
        if (!alwaysShow)
            return (null, DetailTone.Dim);
        // HH:mm alone would read as today; past a day it's just "not a concern".
        return (full - now > FarAhead ? "full >24h" : $"full ~{time(full)}", DetailTone.Good);
    }

    private static string FailureReason(FetchStatus status) => status switch
    {
        FetchStatus.RateLimited => "Rate limited",
        FetchStatus.ServerError => "Server error",
        _ => "Can't reach Claude",
    };

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
