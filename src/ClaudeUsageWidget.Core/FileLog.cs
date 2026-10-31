using System.Globalization;

namespace ClaudeUsageWidget.Core;

/// <summary>Appends timestamped lines to a small, self-rotating text file. Logging never throws.</summary>
public sealed class FileLog(string path, long maxBytes, TimeProvider time, TimeZoneInfo tz)
{
    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeUsageWidget", "widget.log");

    private readonly Lock _lock = new();

    public void Write(string message)
    {
        var stamp = TimeZoneInfo.ConvertTime(time.GetUtcNow(), tz).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"{stamp} {message}{Environment.NewLine}";
        lock (_lock)
        {
            // The lock only covers this process; a second launch can be writing at the same moment,
            // so retry a sharing violation briefly instead of dropping the line.
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    Append(line);
                    return;
                }
                catch (IOException) when (attempt < MaxAttempts)
                {
                    Thread.Sleep(RetryDelay);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A log that can't be written must not take the widget down with it.
                    return;
                }
            }
        }
    }

    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);

    private void Append(string line)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = new FileInfo(path);
        if (existing.Exists && existing.Length + line.Length > maxBytes)
            File.Move(path, path + ".1", overwrite: true);
        File.AppendAllText(path, line);
    }
}
