using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public sealed class CredentialsReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"creds-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Reads_token_and_expiry()
    {
        File.WriteAllText(_path, """{"mcpOAuth":{},"claudeAiOauth":{"accessToken":"tok","refreshToken":"r","expiresAt":1793401348796}}""");

        var creds = CredentialsReader.TryRead(_path);

        Assert.NotNull(creds);
        Assert.Equal("tok", creds.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793401348796), creds.ExpiresAt);
    }

    [Fact]
    public void Missing_file_returns_null()
    {
        Assert.Null(CredentialsReader.TryRead(_path));
    }

    [Fact]
    public void Partial_json_returns_null()
    {
        File.WriteAllText(_path, "{\"claudeAiOauth\":{\"acc");

        Assert.Null(CredentialsReader.TryRead(_path));
    }

    [Fact]
    public void File_locked_returns_null()
    {
        File.WriteAllText(_path, """{"claudeAiOauth":{"accessToken":"tok","expiresAt":1}}""");
        using var lockHandle = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Null(CredentialsReader.TryRead(_path));
    }

    [Fact]
    public void Missing_section_returns_null()
    {
        File.WriteAllText(_path, """{"mcpOAuth":{}}""");

        Assert.Null(CredentialsReader.TryRead(_path));
    }

    [Fact]
    public void Out_of_range_expiry_returns_null()
    {
        File.WriteAllText(_path, """{"claudeAiOauth":{"accessToken":"tok","expiresAt":9223372036854775807}}""");

        Assert.Null(CredentialsReader.TryRead(_path));
    }

    [Fact]
    public void ToString_hides_token()
    {
        var creds = new Credentials("tok-secret", DateTimeOffset.UnixEpoch);

        Assert.DoesNotContain("tok-secret", creds.ToString());
    }
}
