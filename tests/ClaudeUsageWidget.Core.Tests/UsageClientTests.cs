using System.Net;
using ClaudeUsageWidget.Core;
using Microsoft.Extensions.Time.Testing;

namespace ClaudeUsageWidget.Core.Tests;

public sealed class UsageClientTests : IDisposable
{
    private const string OkBody = """{"five_hour":{"utilization":26.0,"resets_at":"2026-10-30T17:09:59+00:00"}}""";

    private readonly string _credsPath = Path.Combine(Path.GetTempPath(), $"creds-{Guid.NewGuid():N}.json");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 16, 0, 0, TimeSpan.Zero));
    private readonly FakeHandler _handler = new();

    public UsageClientTests()
    {
        var expires = new DateTimeOffset(2026, 10, 30, 20, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        File.WriteAllText(_credsPath, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{expires}}}}}""");
    }

    public void Dispose() => File.Delete(_credsPath);

    private UsageClient CreateClient() => new(new HttpClient(_handler), _credsPath, _time);

    [Fact]
    public async Task Ok_returns_usage_and_sends_headers()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody) };

        var result = await CreateClient().FetchAsync();

        Assert.Equal(FetchStatus.Ok, result.Status);
        Assert.Equal(26, result.Usage!.Percent);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.anthropic.com/api/oauth/usage", request.RequestUri!.ToString());
        Assert.Equal("Bearer tok", request.Headers.Authorization!.ToString());
        Assert.Equal("oauth-2025-04-20", Assert.Single(request.Headers.GetValues("anthropic-beta")));
    }

    [Fact]
    public async Task Missing_credentials_is_NotLoggedIn_without_request()
    {
        File.Delete(_credsPath);

        var result = await CreateClient().FetchAsync();

        Assert.Equal(FetchStatus.NotLoggedIn, result.Status);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task Expired_token_is_TokenExpired_without_request()
    {
        _time.SetUtcNow(new DateTimeOffset(2026, 10, 30, 20, 0, 1, TimeSpan.Zero));

        var result = await CreateClient().FetchAsync();

        Assert.Equal(FetchStatus.TokenExpired, result.Status);
        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, FetchStatus.TokenExpired)]
    [InlineData(HttpStatusCode.TooManyRequests, FetchStatus.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, FetchStatus.ServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, FetchStatus.ServerError)]
    [InlineData(HttpStatusCode.NotFound, FetchStatus.Error)]
    public async Task Http_status_maps_to_fetch_status(HttpStatusCode code, FetchStatus expected)
    {
        _handler.Respond = _ => new HttpResponseMessage(code);

        var result = await CreateClient().FetchAsync();

        Assert.Equal(expected, result.Status);
        Assert.Null(result.Usage);
        Assert.Equal((int)code, result.HttpStatus);
    }

    [Fact]
    public async Task Retry_after_seconds_is_reported()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90)) },
        };

        var result = await CreateClient().FetchAsync();

        Assert.Equal(TimeSpan.FromSeconds(90), result.RetryAfter);
    }

    [Fact]
    public async Task Retry_after_date_is_relative_to_now()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(_time.GetUtcNow().AddMinutes(4)) },
        };

        var result = await CreateClient().FetchAsync();

        Assert.Equal(TimeSpan.FromMinutes(4), result.RetryAfter);
    }

    [Fact]
    public async Task Missing_retry_after_is_null()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        Assert.Null((await CreateClient().FetchAsync()).RetryAfter);
    }

    [Fact]
    public async Task Error_body_is_kept_on_one_line_as_detail()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\n  \"error\": {\"type\": \"rate_limit_error\"}\n}"),
        };

        var result = await CreateClient().FetchAsync();

        Assert.Equal("{ \"error\": {\"type\": \"rate_limit_error\"} }", result.Detail);
    }

    [Fact]
    public async Task Long_error_body_is_truncated()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(new string('x', 1000)),
        };

        var result = await CreateClient().FetchAsync();

        Assert.Equal(new string('x', 200) + "…", result.Detail);
    }

    [Fact]
    public async Task Bad_body_is_Error()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") };

        var result = await CreateClient().FetchAsync();

        Assert.Equal(FetchStatus.Error, result.Status);
        Assert.Equal(200, result.HttpStatus);
        Assert.StartsWith("UsageParseException", result.Detail);
    }

    [Fact]
    public async Task Network_failure_is_Error()
    {
        _handler.Respond = _ => throw new HttpRequestException("offline");

        var result = await CreateClient().FetchAsync();

        Assert.Equal(FetchStatus.Error, result.Status);
        Assert.Null(result.HttpStatus);
        Assert.Equal("HttpRequestException: offline", result.Detail);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Respond(request));
        }
    }
}
