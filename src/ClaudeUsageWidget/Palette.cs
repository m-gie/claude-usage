using System.Windows.Media;
using ClaudeUsageWidget.Core;

namespace ClaudeUsageWidget;

internal static class Palette
{
    public static readonly Color Blue = Color.FromRgb(0x5A, 0xA9, 0xFF);
    public static readonly Color Orange = Color.FromRgb(0xFF, 0xB4, 0x54);
    public static readonly Color Red = Color.FromRgb(0xFF, 0x5A, 0x5A);
    public static readonly Color Grey = Color.FromRgb(0x8A, 0x8F, 0x98);

    public static Color For(Level level) => level switch
    {
        Level.Normal => Blue,
        Level.Warning => Orange,
        Level.Critical => Red,
        _ => Grey,
    };
}
