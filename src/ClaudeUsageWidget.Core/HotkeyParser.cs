namespace ClaudeUsageWidget.Core;

/// <summary>Parses "Ctrl+Alt+U" style text into Win32 RegisterHotKey modifiers and virtual-key code.</summary>
public static class HotkeyParser
{
    public const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8;

    public static bool TryParse(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        var keyCount = 0;

        foreach (var part in (text ?? "").Split('+', StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": modifiers |= ModControl; break;
                case "ALT": modifiers |= ModAlt; break;
                case "SHIFT": modifiers |= ModShift; break;
                case "WIN": modifiers |= ModWin; break;
                default:
                    if (!TryParseKey(part.ToUpperInvariant(), out virtualKey))
                        return false;
                    keyCount++;
                    break;
            }
        }

        return modifiers != 0 && keyCount == 1;
    }

    private static bool TryParseKey(string key, out uint vk)
    {
        vk = 0;
        if (key.Length == 1 && (char.IsAsciiLetterUpper(key[0]) || char.IsAsciiDigit(key[0])))
        {
            vk = key[0];
            return true;
        }

        if (key.Length > 1 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var n) && n is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + n - 1);
            return true;
        }

        return false;
    }
}
