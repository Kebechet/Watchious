using System.Collections.Generic;

namespace Watchious.SourceGenerators;

// The generator's view of a Blazor @page template. It accepts the same templates as Watchious.Navigation's
// RouteTemplate, which would otherwise throw when the app starts.
internal sealed class PageRouteTemplate
{
    private static readonly Dictionary<string, string> ConstraintTypes = new()
    {
        ["int"] = "int",
        ["long"] = "long",
        ["bool"] = "bool",
        ["double"] = "double",
        ["float"] = "float",
        ["decimal"] = "decimal",
        ["guid"] = "global::System.Guid",
        ["datetime"] = "global::System.DateTime",
    };

    private PageRouteTemplate(string template, List<Segment> segments)
    {
        Template = template;
        Segments = segments;
    }

    public string Template { get; }

    public List<Segment> Segments { get; }

    public static PageRouteTemplate? TryParse(string template)
    {
        if (!template.StartsWith("/"))
        {
            return null;
        }

        var parts = template.Trim('/').Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        var segments = new List<Segment>();
        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];
            if (!part.StartsWith("{"))
            {
                segments.Add(new Segment(part, null, null, false));
                continue;
            }

            if (!part.EndsWith("}") || part.StartsWith("{*"))
            {
                return null;
            }

            var body = part.Substring(1, part.Length - 2);
            var isOptional = body.EndsWith("?");
            if (isOptional)
            {
                if (index != parts.Length - 1)
                {
                    return null;
                }

                body = body.Substring(0, body.Length - 1);
            }

            var colon = body.IndexOf(':');
            var name = colon < 0 ? body : body.Substring(0, colon);
            var type = "string";
            if (colon >= 0 && !ConstraintTypes.TryGetValue(body.Substring(colon + 1).ToLowerInvariant(), out type))
            {
                return null;
            }

            if (name.Length == 0)
            {
                return null;
            }

            segments.Add(new Segment(null, name, type, isOptional));
        }

        return new PageRouteTemplate(template, segments);
    }

    internal sealed class Segment
    {
        public Segment(string? literal, string? parameterName, string? type, bool isOptional)
        {
            Literal = literal;
            ParameterName = parameterName;
            Type = type;
            IsOptional = isOptional;
        }

        public string? Literal { get; }

        public string? ParameterName { get; }

        public string? Type { get; }

        public bool IsOptional { get; }

        public bool IsValueType => Type is not null && Type != "string";
    }
}
