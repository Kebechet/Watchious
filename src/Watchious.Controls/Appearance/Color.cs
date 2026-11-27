using System.Globalization;

namespace Watchious.Controls;

// MAUI's Color: red, green, blue and alpha from 0 to 1, equal (also with ==) when their ARGB bytes are. Color.FromArgb
// takes a hex string as MAUI's does: #RGB, #ARGB, #RRGGBB or #AARRGGBB, with or without the #.
public sealed record class Color
{
    public Color()
    {
    }

    public Color(float gray)
    {
        Red = Green = Blue = Component(gray);
    }

    public Color(float red, float green, float blue)
    {
        Red = Component(red);
        Green = Component(green);
        Blue = Component(blue);
    }

    public Color(float red, float green, float blue, float alpha)
        : this(red, green, blue)
    {
        Alpha = Component(alpha);
    }

    public Color(byte red, byte green, byte blue)
        : this(red / 255f, green / 255f, blue / 255f)
    {
    }

    public Color(byte red, byte green, byte blue, byte alpha)
        : this(red / 255f, green / 255f, blue / 255f, alpha / 255f)
    {
    }

    public Color(int red, int green, int blue)
        : this(red / 255f, green / 255f, blue / 255f)
    {
    }

    public Color(int red, int green, int blue, int alpha)
        : this(red / 255f, green / 255f, blue / 255f, alpha / 255f)
    {
    }

    public float Red { get; }

    public float Green { get; }

    public float Blue { get; }

    public float Alpha { get; } = 1;

    public static Color FromRgb(int red, int green, int blue)
    {
        return new Color(red, green, blue);
    }

    public static Color FromRgba(int red, int green, int blue, int alpha)
    {
        return new Color(red, green, blue, alpha);
    }

    public static Color FromRgb(double red, double green, double blue)
    {
        return new Color((float)red, (float)green, (float)blue);
    }

    public static Color FromRgba(double red, double green, double blue, double alpha)
    {
        return new Color((float)red, (float)green, (float)blue, (float)alpha);
    }

    public static Color FromUint(uint argb)
    {
        return new Color((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
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

    // As MAUI's: #AARRGGBB when asked for the alpha or when the color isn't opaque, #RRGGBB otherwise.
    public string ToArgbHex(bool includeAlpha = false)
    {
        return includeAlpha || Alpha < 1
            ? $"#{ToByte(Alpha):X2}{ToByte(Red):X2}{ToByte(Green):X2}{ToByte(Blue):X2}"
            : $"#{ToByte(Red):X2}{ToByte(Green):X2}{ToByte(Blue):X2}";
    }

    public string ToRgbaHex(bool includeAlpha = false)
    {
        return includeAlpha || Alpha < 1
            ? $"#{ToByte(Red):X2}{ToByte(Green):X2}{ToByte(Blue):X2}{ToByte(Alpha):X2}"
            : $"#{ToByte(Red):X2}{ToByte(Green):X2}{ToByte(Blue):X2}";
    }

    public bool Equals(Color? other)
    {
        return other is not null && ToArgb() == other.ToArgb();
    }

    public override int GetHashCode()
    {
        return ToArgb();
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"[Color: Red={Red}, Green={Green}, Blue={Blue}, Alpha={Alpha}]");
    }

    // #RRGGBBAA, the order CSS and the hosts read.
    internal string ToProtocol()
    {
        return ToRgbaHex(includeAlpha: true);
    }

    private int ToArgb()
    {
        return (ToByte(Alpha) << 24) | (ToByte(Red) << 16) | (ToByte(Green) << 8) | ToByte(Blue);
    }

    // NaN, which Math.Clamp keeps, would reach the hosts as an invalid color.
    private static float Component(float value)
    {
        return float.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
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
