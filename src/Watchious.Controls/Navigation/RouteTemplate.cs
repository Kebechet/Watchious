using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Watchious.Navigation;

// A Blazor @page template: literal segments and {Name} or {Name:constraint} parameters, the last of which may be
// optional ({Name?}). Constraints: int, long, bool, double, float, decimal, guid, datetime.
internal sealed class RouteTemplate
{
    private readonly Segment[] _segments;

    private RouteTemplate(string template, Segment[] segments)
    {
        Template = template;
        _segments = segments;
    }

    public string Template { get; }

    public bool HasParameters => _segments.Any(segment => segment.ParameterName is not null);

    // Higher first. Fewer optional parameters is higher, hence the negative count.
    public (int Literals, int ConstrainedParameters, int NegativeOptionalParameters, int Segments) Precedence => (
        _segments.Count(segment => segment.ParameterName is null),
        _segments.Count(segment => segment.Constraint is not null),
        -_segments.Count(segment => segment.IsOptional),
        _segments.Length);

    public static RouteTemplate Parse(string template)
    {
        if (!template.StartsWith('/'))
        {
            throw new ArgumentException($"The route \"{template}\" must start with '/'.", nameof(template));
        }

        var parts = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segments = new Segment[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            segments[index] = Segment.Parse(template, parts[index]);
            if (segments[index].IsOptional && index != parts.Length - 1)
            {
                throw new ArgumentException($"Only the last parameter of \"{template}\" can be optional.", nameof(template));
            }
        }

        return new RouteTemplate(template, segments);
    }

    public bool TryMatch(string path, [NotNullWhen(true)] out Dictionary<string, object?>? parameters)
    {
        parameters = null;
        var parts = path.Split('?', '#')[0].Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var requiredCount = _segments.Length > 0 && _segments[^1].IsOptional ? _segments.Length - 1 : _segments.Length;
        if (parts.Length < requiredCount || parts.Length > _segments.Length)
        {
            return false;
        }

        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < _segments.Length; index++)
        {
            var segment = _segments[index];
            if (index >= parts.Length)
            {
                values[segment.ParameterName!] = null;
                continue;
            }

            var part = Uri.UnescapeDataString(parts[index]);
            if (segment.ParameterName is null)
            {
                if (!string.Equals(segment.Literal, part, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                continue;
            }

            if (!segment.TryConvert(part, out var value))
            {
                return false;
            }

            values[segment.ParameterName] = value;
        }

        parameters = values;
        return true;
    }

    private sealed class Segment
    {
        private Segment(string? literal, string? parameterName, string? constraint, bool isOptional)
        {
            Literal = literal;
            ParameterName = parameterName;
            Constraint = constraint;
            IsOptional = isOptional;
        }

        public string? Literal { get; }

        public string? ParameterName { get; }

        public string? Constraint { get; }

        public bool IsOptional { get; }

        public static Segment Parse(string template, string part)
        {
            if (!part.StartsWith('{'))
            {
                return new Segment(part, null, null, false);
            }

            if (!part.EndsWith('}') || part.StartsWith("{*", StringComparison.Ordinal))
            {
                throw new ArgumentException($"The route \"{template}\" has an unsupported segment \"{part}\".", nameof(template));
            }

            var body = part[1..^1];
            var isOptional = body.EndsWith('?');
            if (isOptional)
            {
                body = body[..^1];
            }

            var colon = body.IndexOf(':');
            var name = colon < 0 ? body : body[..colon];
            var constraint = colon < 0 ? null : body[(colon + 1)..].ToLowerInvariant();
            if (constraint is not (null or "int" or "long" or "bool" or "double" or "float" or "decimal" or "guid" or "datetime"))
            {
                throw new ArgumentException($"The route \"{template}\" has an unsupported constraint \"{constraint}\".", nameof(template));
            }

            return new Segment(null, name, constraint, isOptional);
        }

        public bool TryConvert(string text, out object? value)
        {
            var culture = CultureInfo.InvariantCulture;
            bool isValid;
            switch (Constraint)
            {
                case "int":
                    isValid = int.TryParse(text, NumberStyles.Integer, culture, out var intValue);
                    value = intValue;
                    break;
                case "long":
                    isValid = long.TryParse(text, NumberStyles.Integer, culture, out var longValue);
                    value = longValue;
                    break;
                case "bool":
                    isValid = bool.TryParse(text, out var boolValue);
                    value = boolValue;
                    break;
                case "double":
                    isValid = double.TryParse(text, NumberStyles.Float, culture, out var doubleValue);
                    value = doubleValue;
                    break;
                case "float":
                    isValid = float.TryParse(text, NumberStyles.Float, culture, out var floatValue);
                    value = floatValue;
                    break;
                case "decimal":
                    isValid = decimal.TryParse(text, NumberStyles.Number, culture, out var decimalValue);
                    value = decimalValue;
                    break;
                case "guid":
                    isValid = Guid.TryParse(text, out var guidValue);
                    value = guidValue;
                    break;
                case "datetime":
                    isValid = DateTime.TryParse(text, culture, DateTimeStyles.RoundtripKind, out var dateValue);
                    value = dateValue;
                    break;
                default:
                    isValid = true;
                    value = text;
                    break;
            }

            return isValid;
        }
    }
}
