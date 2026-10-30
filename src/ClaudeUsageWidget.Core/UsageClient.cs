using System.Net;
using System.Net.Http.Headers;

namespace ClaudeUsageWidget.Core;

public enum FetchStatus { Ok, NotLoggedIn, TokenExpired, RateLimited, Error }

public sealed record FetchResult(FetchStatus Status, SessionUsage? Usage);

public sealed class UsageClient(HttpClient http, string credentialsPath, TimeProvider time)
{
    private static readonly Uri Endpoint = new("https://api.anthropic.com/api/oauth/usage");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<FetchResult> FetchAsync(CancellationToken ct = default)
    {
        // Re-read every time: Claude Code rewrites the file when it refreshes the token.
        var creds = CredentialsReader.TryRead(credentialsPath);
        if (creds is null)
            return new FetchResult(FetchStatus.NotLoggedIn, null);
        if (creds.ExpiresAt <= time.GetUtcNow())
            return new FetchResult(FetchStatus.TokenExpired, null);

        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd("claude-usage-widget/1.0");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return new FetchResult(FetchStatus.TokenExpired, null);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                return new FetchResult(FetchStatus.RateLimited, null);
            if (!response.IsSuccessStatusCode)
                return new FetchResult(FetchStatus.Error, null);

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return new FetchResult(FetchStatus.Ok, UsageParser.Parse(body));
        }
        catch (Exception ex) when (ex is HttpRequestException or UsageParseException
                                   || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return new FetchResult(FetchStatus.Error, null);
        }
    }
}
