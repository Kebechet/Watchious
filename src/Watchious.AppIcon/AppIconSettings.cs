using System.Globalization;
using SkiaSharp;

namespace Watchious.AppIcon;

// A WatchiousAppIcon item: the picture (its Include), how much of the icon's width it takes, the color it is painted
// in, and what is behind it.
public sealed record AppIconSettings(
    string Foreground,
    float ForegroundSize,
    SKColor? ForegroundTintColor,
    SKColor BackgroundColor,
    string? BackgroundFile)
{
    public const float DefaultForegroundSize = 0.65f;

    // A watch's icon is opaque, so something has to be behind a picture with transparent parts.
    public static readonly SKColor DefaultBackgroundColor = SKColors.Black;

    // "65%": a share of the icon's width, from 1% to 100%.
    public static float ParseSize(string text)
    {
        var trimmed = text.Trim();
        var isPercentage = trimmed.EndsWith('%');
        var number = isPercentage ? trimmed[..^1] : trimmed;
        var isNumber = float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent);
        if (!isPercentage || !isNumber || percent < 1 || percent > 100)
        {
            throw new AppIconException($"ForegroundSize '{text}' isn't a percentage from 1% to 100%, such as 65%.");
        }

        return percent / 100;
    }

    // As MAUI's colors: #RGB, #RRGGBB, or #AARRGGBB with the alpha first.
    public static SKColor ParseColor(string text, string setting)
    {
        var digits = text.Trim().TrimStart('#');
        var isHex = digits.Length is 3 or 6 or 8 && digits.All(char.IsAsciiHexDigit);
        if (!isHex)
        {
            throw new AppIconException($"{setting} '{text}' isn't a color: use #RRGGBB, #AARRGGBB or #RGB.");
        }

        if (digits.Length == 3)
        {
            digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        }

        if (digits.Length == 6)
        {
            digits = "FF" + digits;
        }

        var argb = uint.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new SKColor(argb);
    }
}

// A setting or a file the icon can't be drawn from, reported as a build error.
public sealed class AppIconException(string message) : Exception(message);
