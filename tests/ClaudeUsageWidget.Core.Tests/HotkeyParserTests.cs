using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget.Core.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void Hotkey_default()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+U", out var mods, out var vk));
        Assert.Equal(0x3u, mods);
        Assert.Equal(0x55u, vk);
    }

    [Fact]
    public void Hotkey_f_key()
    {
        Assert.True(HotkeyParser.TryParse("shift + win + f5", out var mods, out var vk));
        Assert.Equal(0xCu, mods);
        Assert.Equal(0x74u, vk);
    }

    [Fact]
    public void Hotkey_digit()
    {
        Assert.True(HotkeyParser.TryParse("Control+7", out var mods, out var vk));
        Assert.Equal(0x2u, mods);
        Assert.Equal((uint)'7', vk);
    }

    [Theory]
    [InlineData("U")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+F25")]
    [InlineData("")]
    public void Hotkey_invalid(string text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _, out _));
    }
}
