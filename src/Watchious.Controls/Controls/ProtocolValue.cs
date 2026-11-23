using System.Globalization;

namespace Watchious.Controls;

// Blazor turns every element attribute value into a string with the current culture, and drops false
// booleans entirely, which would make IsEnabled="false" look like "not set" to the native host. Controls
// therefore format values themselves: booleans as "true"/"false", numbers culture-invariant.
internal static class ProtocolValue
{
    public static string Of(bool value)
    {
        return value ? "true" : "false";
    }

    public static string? Of(double? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture);
    }

    public static string Of(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParseInt(string? value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    public static bool ToBool(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
