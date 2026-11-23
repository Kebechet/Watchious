using System.ComponentModel;
using System.Globalization;

namespace Watchious.Navigation;

// Formats route parameters for the generated Routes helpers, the way RouteTemplate parses them back.
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RouteValue
{
    public static string Format(string value)
    {
        return Uri.EscapeDataString(value);
    }

    public static string Format(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Format(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Format(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Format(float value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Format(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Format(bool value)
    {
        return value ? "true" : "false";
    }

    public static string Format(Guid value)
    {
        return value.ToString("D");
    }

    public static string Format(DateTime value)
    {
        // The round-trip format keeps the fractions of a second and the kind (UTC, local).
        return Uri.EscapeDataString(value.ToString("o", CultureInfo.InvariantCulture));
    }
}
