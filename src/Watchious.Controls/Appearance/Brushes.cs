using System.Globalization;
using System.Text;

namespace Watchious.Controls;

// MAUI's brushes for a Background. A brush is immutable here: a page sets a new one to change its background, so the
// change raises PropertyChanged.
public abstract class Brush
{
    private protected Brush()
    {
    }

    // The background as the hosts read it: a color as #RRGGBBAA, or "linear x1 y1 x2 y2" / "radial cx cy r" followed
    // by each stop's color and offset. Null for no background.
    internal abstract string? ToProtocol();
}

// Equal when their colors are, as MAUI's.
public sealed class SolidColorBrush(Color color) : Brush
{
    public Color Color { get; } = color;

    public override bool Equals(object? obj)
    {
        var other = obj as SolidColorBrush;
        return other is not null && Color == other.Color;
    }

    public override int GetHashCode()
    {
        return Color.GetHashCode();
    }

    internal override string? ToProtocol()
    {
        return Color.ToProtocol();
    }
}

// A color at an offset along the gradient, from 0 to 1. Equal when the colors are and the offsets nearly, as MAUI's.
public sealed class GradientStop(Color color, float offset)
{
    public Color Color { get; } = color;

    public float Offset { get; } = float.IsNaN(offset) ? 0 : Math.Clamp(offset, 0, 1);

    public override bool Equals(object? obj)
    {
        var other = obj as GradientStop;
        return other is not null && Color == other.Color && Math.Abs(Offset - other.Offset) < 0.00001;
    }

    public override int GetHashCode()
    {
        return Color.GetHashCode();
    }
}

// A point relative to the page: (0, 0) its top left, (1, 1) its bottom right.
public readonly record struct Point(double X, double Y);

public abstract class GradientBrush : Brush
{
    // A copy, so that a change to the caller's list can't change the brush without raising PropertyChanged.
    private protected GradientBrush(IReadOnlyList<GradientStop> gradientStops)
    {
        GradientStops = gradientStops.ToList().AsReadOnly();
    }

    public IReadOnlyList<GradientStop> GradientStops { get; }

    // A gradient with one stop is that stop's color. One without any is empty, as in MAUI: the page's BackgroundColor
    // shows instead, or the theme's background.
    private protected string? GradientToProtocol(string shape)
    {
        if (GradientStops.Count == 0)
        {
            return null;
        }

        if (GradientStops.Count == 1)
        {
            return GradientStops[0].Color.ToProtocol();
        }

        var builder = new StringBuilder(shape);
        foreach (var stop in GradientStops.OrderBy(stop => stop.Offset))
        {
            builder.Append(' ').Append(stop.Color.ToProtocol()).Append(' ').Append(Number(stop.Offset));
        }

        return builder.ToString();
    }

    // NaN or infinity, which no host could draw, as 0.
    private protected static string Number(double value)
    {
        return double.IsFinite(value) ? value.ToString("0.####", CultureInfo.InvariantCulture) : "0";
    }
}

// MAUI's LinearGradientBrush: from StartPoint to EndPoint, by default from the top left to the bottom right.
public sealed class LinearGradientBrush(IReadOnlyList<GradientStop> gradientStops, Point startPoint, Point endPoint)
    : GradientBrush(gradientStops)
{
    public LinearGradientBrush(IReadOnlyList<GradientStop> gradientStops)
        : this(gradientStops, new Point(0, 0), new Point(1, 1))
    {
    }

    public Point StartPoint { get; } = startPoint;

    public Point EndPoint { get; } = endPoint;

    internal override string? ToProtocol()
    {
        return GradientToProtocol($"linear {Number(StartPoint.X)} {Number(StartPoint.Y)} {Number(EndPoint.X)} {Number(EndPoint.Y)}");
    }
}

// MAUI's RadialGradientBrush: circles around Center, with Radius a fraction of the page's longer side; by default from
// the middle, 0.5 reaching the two edges across the longer side.
public sealed class RadialGradientBrush(IReadOnlyList<GradientStop> gradientStops, Point center, double radius)
    : GradientBrush(gradientStops)
{
    public RadialGradientBrush(IReadOnlyList<GradientStop> gradientStops)
        : this(gradientStops, new Point(0.5, 0.5), 0.5)
    {
    }

    public Point Center { get; } = center;

    public double Radius { get; } = double.IsNaN(radius) ? 0 : Math.Max(radius, 0);

    internal override string? ToProtocol()
    {
        return GradientToProtocol($"radial {Number(Center.X)} {Number(Center.Y)} {Number(Radius)}");
    }
}
