using System.Globalization;

namespace Watchious.Controls;

// MAUI's Color: red, green, blue and alpha from 0 to 1. Color.FromArgb takes a hex string as MAUI's does: #RGB, #ARGB,
// #RRGGBB or #AARRGGBB, with or without the #.
public sealed class Color : IEquatable<Color>
{
    public Color(float red, float green, float blue, float alpha = 1)
    {
        Red = Math.Clamp(red, 0, 1);
        Green = Math.Clamp(green, 0, 1);
        Blue = Math.Clamp(blue, 0, 1);
        Alpha = Math.Clamp(alpha, 0, 1);
    }

    public float Red { get; }

    public float Green { get; }

    public float Blue { get; }

    public float Alpha { get; }

    public static Color FromRgb(int red, int green, int blue)
    {
        return FromRgba(red, green, blue, 255);
    }

    public static Color FromRgba(int red, int green, int blue, int alpha)
    {
        return new Color(red / 255f, green / 255f, blue / 255f, alpha / 255f);
    }

    public static Color FromRgb(double red, double green, double blue)
    {
        return new Color((float)red, (float)green, (float)blue);
    }

    public static Color FromRgba(double red, double green, double blue, double alpha)
    {
        return new Color((float)red, (float)green, (float)blue, (float)alpha);
    }

    public static Color FromArgb(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var digits = hex.StartsWith('#') ? hex[1..] : hex;
        if (!digits.All(char.IsAsciiHexDigit))
        {
            throw new FormatException($"'{hex}' is not a color: use #RGB, #ARGB, #RRGGBB or #AARRGGBB.");
        }

        return digits.Length switch
        {
            3 => FromRgba(Nibble(digits[0]), Nibble(digits[1]), Nibble(digits[2]), 255),
            4 => FromRgba(Nibble(digits[1]), Nibble(digits[2]), Nibble(digits[3]), Nibble(digits[0])),
            6 => FromRgba(Byte(digits, 0), Byte(digits, 2), Byte(digits, 4), 255),
            8 => FromRgba(Byte(digits, 2), Byte(digits, 4), Byte(digits, 6), Byte(digits, 0)),
            _ => throw new FormatException($"'{hex}' is not a color: use #RGB, #ARGB, #RRGGBB or #AARRGGBB."),
        };
    }

    // #AARRGGBB, as MAUI's ToArgbHex(includeAlpha: true) writes it.
    public string ToArgbHex()
    {
        return $"#{ToByte(Alpha):X2}{ToByte(Red):X2}{ToByte(Green):X2}{ToByte(Blue):X2}";
    }

    public bool Equals(Color? other)
    {
        return other is not null && ToArgbHex() == other.ToArgbHex();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Color);
    }

    public override int GetHashCode()
    {
        return ToArgbHex().GetHashCode(StringComparison.Ordinal);
    }

    public override string ToString()
    {
        return ToArgbHex();
    }

    // #RRGGBBAA, the order CSS and the hosts read.
    internal string ToProtocol()
    {
        return $"#{ToByte(Red):X2}{ToByte(Green):X2}{ToByte(Blue):X2}{ToByte(Alpha):X2}";
    }

    private static int Nibble(char digit)
    {
        return int.Parse(new string(digit, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static int Byte(string digits, int start)
    {
        return int.Parse(digits.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static int ToByte(float component)
    {
        return (int)MathF.Round(component * 255);
    }
}
