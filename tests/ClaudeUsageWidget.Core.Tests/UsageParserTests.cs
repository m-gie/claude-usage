using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public class UsageParserTests
{
    [Fact]
    public void Parses_five_hour()
    {
        const string json = """
            {"five_hour":{"utilization":26.0,"resets_at":"2026-10-30T17:09:59.955594+00:00","limit_dollars":null},
             "seven_day":{"utilization":6.0,"resets_at":"2026-11-05T14:59:59.955613+00:00"},
             "extra":1,"limits":[{"kind":"session"}]}
            """;

        var usage = UsageParser.Parse(json);

        Assert.Equal(26, usage.Percent);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 17, 9, 59, 955, TimeSpan.Zero).AddTicks(5940), usage.ResetsAt);
    }

    [Fact]
    public void Null_five_hour_is_zero_without_reset()
    {
        var usage = UsageParser.Parse("""{"five_hour":null}""");

        Assert.Equal(new SessionUsage(0, null), usage);
    }

    [Fact]
    public void Missing_five_hour_throws()
    {
        Assert.Throws<UsageParseException>(() => UsageParser.Parse("""{"seven_day":{}}"""));
    }

    [Fact]
    public void Invalid_json_throws()
    {
        Assert.Throws<UsageParseException>(() => UsageParser.Parse("<html>"));
    }

    [Fact]
    public void Null_resets_at_is_allowed()
    {
        var usage = UsageParser.Parse("""{"five_hour":{"utilization":5,"resets_at":null}}""");

        Assert.Equal(new SessionUsage(5, null), usage);
    }
}
