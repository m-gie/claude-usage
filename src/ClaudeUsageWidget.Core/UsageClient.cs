using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace ClaudeUsageWidget.Core;

public enum FetchStatus { Ok, NotLoggedIn, TokenExpired, RateLimited, ServerError, Error }

/// <summary>Outcome of one poll. HttpStatus, RetryAfter and Detail are diagnostics for the log.</summary>
public sealed record FetchResult(FetchStatus Status, SessionUsage? Usage,
    int? HttpStatus = null, TimeSpan? RetryAfter = null, string? Detail = null);

public sealed partial class UsageClient(HttpClient http, string credentialsPath, TimeProvider time)
{
    private static readonly Uri Endpoint = new("https://api.anthropic.com/api/oauth/usage");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private const int MaxDetailLength = 200;

    public async Task<FetchResult> FetchAsync(CancellationToken ct = default)
    {
        // Re-read every time: Claude Code rewrites the file when it refreshes the token.
        var creds = CredentialsReader.TryRead(credentialsPath);
        if (creds is null)
            return new FetchResult(FetchStatus.NotLoggedIn, null);
        if (creds.ExpiresAt <= time.GetUtcNow())
            return new FetchResult(FetchStatus.TokenExpired, null, Detail: $"token expired {creds.ExpiresAt:O}");

        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd("claude-usage-widget/1.0");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        int? code = null;
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            code = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (response.IsSuccessStatusCode)
                return new FetchResult(FetchStatus.Ok, UsageParser.Parse(body), code);

            var status = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => FetchStatus.TokenExpired,
                HttpStatusCode.TooManyRequests => FetchStatus.RateLimited,
                _ when code >= 500 => FetchStatus.ServerError,
                _ => FetchStatus.Error,
            };
            return new FetchResult(status, null, code, RetryAfter(response.Headers.RetryAfter), OneLine(body));
        }
        catch (Exception ex) when (ex is HttpRequestException or UsageParseException
                                   || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            var detail = ex is OperationCanceledException ? $"timed out after {Timeout.TotalSeconds:0}s" : OneLine($"{ex.GetType().Name}: {ex.Message}");
            return new FetchResult(FetchStatus.Error, null, code, Detail: detail);
        }
    }

    private TimeSpan? RetryAfter(RetryConditionHeaderValue? header) => header switch
    {
        { Delta: { } delta } => delta,
        { Date: { } date } => date - time.GetUtcNow(),
        _ => null,
    };

    private static string? OneLine(string text)
    {
        var line = Whitespace().Replace(text, " ").Trim();
        if (line.Length == 0)
            return null;
        return line.Length > MaxDetailLength ? line[..MaxDetailLength] + "…" : line;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
