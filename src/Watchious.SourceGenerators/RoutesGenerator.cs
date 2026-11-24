using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Watchious.SourceGenerators;

// Typed routes for an app's pages, e.g. Routes.Workout(exerciseId: 5) for @page "/workout/{ExerciseId:int}", and a
// class that registers every route with the app builder (see WatchiousRoutesAttribute).
//
// Razor compiles .razor files with its own source generator, whose output other generators can't see, so the
// @page and @namespace directives are read from the .razor files themselves, which the Razor SDK passes to
// generators as additional files. Pages written in C# are found by their [Route] attribute.
[Generator(LanguageNames.CSharp)]
public sealed class RoutesGenerator : IIncrementalGenerator
{
    private const string RouteAttributeName = "Microsoft.AspNetCore.Components.RouteAttribute";
    private const string RoutesInterfaceName = "Watchious.Navigation.IWatchiousRoutes";

    private static readonly DiagnosticDescriptor DuplicateHelper = new(
        "WATCHIOUS001",
        "Duplicate route helper",
        "Routes.{0}({1}) already exists for another page, so the route \"{2}\" of {3} gets no Routes helper",
        "Watchious.Navigation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedRoute = new(
        "WATCHIOUS002",
        "Unsupported route",
        "Watchious navigation doesn't support the route \"{0}\" of {1}: use literal segments and {{Name}} or {{Name:constraint}} parameters (int, long, bool, double, float, decimal, guid, datetime), of which only the last can be optional",
        "Watchious.Navigation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Blazor fails at start-up for such routes ("The following routes are ambiguous").
    private static readonly DiagnosticDescriptor SameRoute = new(
        "WATCHIOUS004",
        "Same route",
        "The route \"{0}\" of {1} matches the same addresses as \"{2}\" of {3}, so navigation can't tell them apart",
        "Watchious.Navigation",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OverlappingRoutes = new(
        "WATCHIOUS005",
        "Overlapping routes",
        "The route \"{0}\" of {1} and \"{2}\" of {3} can match the same address, and neither takes precedence: give one of them a literal of its own",
        "Watchious.Navigation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Number constraints whose values overlap: 12 is an int, a long and a double.
    private static readonly HashSet<string> NumberTypes = new() { "int", "long", "double", "float", "decimal" };

    private static readonly Regex PageDirective = new(@"^\s*@page\s+""(?<route>[^""]*)""", RegexOptions.Multiline);
    private static readonly Regex NamespaceDirective = new(@"^\s*@namespace\s+(?<namespace>[\w.]+)", RegexOptions.Multiline);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var razorFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((pair, cancellationToken) => ReadRazorFile(pair.Left, pair.Right.GetOptions(pair.Left), cancellationToken))
            .Collect();

        var classPages = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RouteAttributeName,
                (node, _) => node is ClassDeclarationSyntax,
                (syntax, _) => ReadClassPage(syntax))
            .Collect();

        var rootNamespace = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var value) ? value : "");

        var referencesWatchious = context.CompilationProvider.Select((compilation, _) =>
            compilation.GetTypeByMetadataName(RoutesInterfaceName) is not null);

        var input = razorFiles.Combine(classPages).Combine(rootNamespace).Combine(referencesWatchious);
        context.RegisterSourceOutput(input, (output, source) =>
        {
            var (((files, classes), ns), hasWatchious) = source;
            if (hasWatchious)
            {
                Generate(output, files, classes, ns);
            }
        });
    }

    private static RazorFile ReadRazorFile(AdditionalText file, AnalyzerConfigOptions options, CancellationToken cancellationToken)
    {
        var text = file.GetText(cancellationToken)?.ToString() ?? "";
        var targetPath = options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var value) && value.Length > 0
            ? DecodeTargetPath(value)
            : System.IO.Path.GetFileName(file.Path);
        var namespaceMatch = NamespaceDirective.Match(text);
        var routes = PageDirective.Matches(text).Cast<Match>().Select(match => match.Groups["route"].Value);
        return new RazorFile(
            file.Path,
            targetPath.Replace('\\', '/'),
            namespaceMatch.Success ? namespaceMatch.Groups["namespace"].Value : null,
            string.Join("\n", routes));
    }

    // The Razor SDK passes the project-relative path base64-encoded, and its generator decodes it the same way.
    private static string DecodeTargetPath(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private static ClassPage ReadClassPage(GeneratorAttributeSyntaxContext syntax)
    {
        var type = (INamedTypeSymbol)syntax.TargetSymbol;
        var routes = syntax.Attributes
            .Select(attribute => attribute.ConstructorArguments.FirstOrDefault().Value as string)
            .Where(route => route is not null);
        return new ClassPage(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.Name,
            string.Join("\n", routes),
            syntax.TargetNode.GetLocation().GetLineSpan());
    }

    private static void Generate(
        SourceProductionContext output,
        ImmutableArray<RazorFile> razorFiles,
        ImmutableArray<ClassPage> classPages,
        string rootNamespace)
    {
        var pages = new List<Page>();
        foreach (var file in razorFiles.Where(file => !file.IsImports && file.Routes.Length > 0))
        {
            var className = Sanitize(System.IO.Path.GetFileNameWithoutExtension(file.TargetPath));
            var ns = RazorNamespace(file, razorFiles, rootNamespace);
            var fullName = "global::" + (ns.Length > 0 ? ns + "." : "") + className;
            var location = Location.Create(file.Path, default, default);
            pages.Add(new Page(fullName, className, file.Routes.Split('\n'), location));
        }

        foreach (var page in classPages.Where(page => page.Routes.Length > 0))
        {
            var location = Location.Create(page.Location.Path, default, page.Location.Span);
            pages.Add(new Page(page.FullName, page.ClassName, page.Routes.Split('\n'), location));
        }

        if (pages.Count == 0)
        {
            return;
        }

        var helpers = new StringBuilder();
        var registrations = new StringBuilder();
        var signatures = new HashSet<string>();
        var sameRoutes = new Dictionary<string, (string Route, Page Page)>();
        var overlappingRoutes = new Dictionary<string, (string Route, Page Page)>();
        foreach (var page in pages.OrderBy(page => page.FullName, StringComparer.Ordinal))
        {
            foreach (var route in page.Routes)
            {
                var template = PageRouteTemplate.TryParse(route);
                if (template is null)
                {
                    output.ReportDiagnostic(Diagnostic.Create(UnsupportedRoute, page.Location, route, page.ClassName));
                    continue;
                }

                var sameKey = RouteKey(template, groupNumbers: false);
                var overlapKey = RouteKey(template, groupNumbers: true);
                if (sameRoutes.TryGetValue(sameKey, out var same))
                {
                    output.ReportDiagnostic(Diagnostic.Create(SameRoute, page.Location, route, page.ClassName, same.Route, same.Page.ClassName));
                }
                else if (overlappingRoutes.TryGetValue(overlapKey, out var overlapping))
                {
                    output.ReportDiagnostic(Diagnostic.Create(OverlappingRoutes, page.Location, route, page.ClassName, overlapping.Route, overlapping.Page.ClassName));
                }

                sameRoutes[sameKey] = (route, page);
                overlappingRoutes[overlapKey] = (route, page);

                registrations.Append("            builder.AddRoute<").Append(page.FullName).Append(">(")
                    .Append(SymbolDisplay.FormatLiteral(route, quote: true)).AppendLine(");");

                var helperName = HelperName(page.ClassName);
                var parameters = template.Segments.Where(segment => segment.ParameterName is not null).ToList();
                var parameterTypes = string.Join(", ", parameters.Select(parameter => parameter.Type));
                if (!signatures.Add(helperName + "(" + parameterTypes + ")"))
                {
                    output.ReportDiagnostic(Diagnostic.Create(DuplicateHelper, page.Location, helperName, parameterTypes, route, page.ClassName));
                    continue;
                }

                AppendHelper(helpers, page, template, helperName, parameters);
            }
        }

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("[assembly: global::Watchious.Navigation.WatchiousRoutesAttribute(typeof(global::Watchious.Generated.WatchiousRoutes))]");
        source.AppendLine();
        var hasNamespace = rootNamespace.Length > 0;
        if (hasNamespace)
        {
            source.Append("namespace ").AppendLine(rootNamespace).AppendLine("{");
        }

        source.AppendLine("    /// <summary>The routes of the app's pages, from their @page directives.</summary>");
        source.AppendLine("    public static partial class Routes");
        source.AppendLine("    {");
        source.Append(helpers.ToString().TrimEnd()).AppendLine();
        source.AppendLine("    }");
        if (hasNamespace)
        {
            source.AppendLine("}");
        }

        source.AppendLine();
        source.AppendLine("namespace Watchious.Generated");
        source.AppendLine("{");
        source.AppendLine("    internal sealed class WatchiousRoutes : global::Watchious.Navigation.IWatchiousRoutes");
        source.AppendLine("    {");
        source.AppendLine("        public void AddTo(global::Watchious.Hosting.WatchiousAppBuilder builder)");
        source.AppendLine("        {");
        source.Append(registrations);
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
        output.AddSource("WatchiousRoutes.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    private static void AppendHelper(
        StringBuilder helpers,
        Page page,
        PageRouteTemplate template,
        string helperName,
        List<PageRouteTemplate.Segment> parameters)
    {
        var declarations = parameters.Select(parameter => parameter.IsOptional
            ? $"{parameter.Type}? {ParameterName(parameter)} = null"
            : $"{parameter.Type} {ParameterName(parameter)}");
        var last = template.Segments.LastOrDefault();
        var expression = last is not null && last.IsOptional
            ? $"{ParameterName(last)} is null ? {Concatenate(template.Segments.Take(template.Segments.Count - 1))} : {Concatenate(template.Segments)}"
            : Concatenate(template.Segments);

        helpers.Append("        /// <summary>The route <c>").Append(EscapeXml(template.Template)).Append("</c> of <see cref=\"")
            .Append(page.FullName).AppendLine("\"/>.</summary>");
        helpers.Append("        public static string ").Append(helperName).Append('(').Append(string.Join(", ", declarations)).AppendLine(")");
        helpers.AppendLine("        {");
        helpers.Append("            return ").Append(expression).AppendLine(";");
        helpers.AppendLine("        }");
        helpers.AppendLine();
    }

    // "/summary/" + RouteValue.Format(count), merging literal segments into one string.
    // What decides which addresses a route matches: its literals, compared without case as navigation does, and the
    // type and optionality of each parameter. With groupNumbers, number types count as one, since they overlap.
    private static string RouteKey(PageRouteTemplate template, bool groupNumbers)
    {
        return string.Join("/", template.Segments.Select(segment =>
        {
            if (segment.Literal is not null)
            {
                return "=" + segment.Literal.ToLowerInvariant();
            }

            var type = groupNumbers && NumberTypes.Contains(segment.Type!) ? "number" : segment.Type;
            return segment.IsOptional ? type + "?" : type;
        }));
    }

    private static string Concatenate(IEnumerable<PageRouteTemplate.Segment> segments)
    {
        var pieces = new List<string>();
        var literal = new StringBuilder();
        foreach (var segment in segments)
        {
            literal.Append('/');
            if (segment.ParameterName is null)
            {
                literal.Append(segment.Literal);
                continue;
            }

            pieces.Add(SymbolDisplay.FormatLiteral(literal.ToString(), quote: true));
            literal.Clear();
            var value = ParameterName(segment) + (segment.IsOptional && segment.IsValueType ? ".Value" : "");
            pieces.Add($"global::Watchious.Navigation.RouteValue.Format({value})");
        }

        if (literal.Length > 0)
        {
            pieces.Add(SymbolDisplay.FormatLiteral(literal.ToString(), quote: true));
        }

        return pieces.Count == 0 ? "\"/\"" : string.Join(" + ", pieces);
    }

    // Razor's namespace for a component: its own @namespace; else the nearest _Imports.razor @namespace above it plus
    // the folders below that _Imports.razor; else the root namespace plus all of its folders.
    private static string RazorNamespace(RazorFile file, ImmutableArray<RazorFile> files, string rootNamespace)
    {
        if (file.Namespace is not null)
        {
            return file.Namespace;
        }

        var folders = Folders(file.TargetPath);
        for (var depth = folders.Count; depth >= 0; depth--)
        {
            var importsPath = string.Concat(folders.Take(depth).Select(folder => folder + "/")) + "_Imports.razor";
            var imports = files.FirstOrDefault(candidate =>
                candidate.IsImports && string.Equals(candidate.TargetPath, importsPath, StringComparison.OrdinalIgnoreCase));
            if (imports?.Namespace is not null)
            {
                return Join(imports.Namespace, folders.Skip(depth));
            }
        }

        return Join(rootNamespace, folders);
    }

    private static List<string> Folders(string targetPath)
    {
        var parts = targetPath.Split('/');
        return parts.Take(parts.Length - 1).Where(part => part.Length > 0 && part != ".").ToList();
    }

    private static string Join(string ns, IEnumerable<string> folders)
    {
        var parts = new[] { ns }.Concat(folders.Select(Sanitize)).Where(part => part.Length > 0);
        return string.Join(".", parts);
    }

    // Razor turns file and folder names into identifiers the same way.
    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length + 1);
        if (name.Length == 0 || !SyntaxFacts.IsIdentifierStartCharacter(name[0]))
        {
            builder.Append('_');
        }

        foreach (var character in name)
        {
            builder.Append(SyntaxFacts.IsIdentifierPartCharacter(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static string HelperName(string className)
    {
        return className.EndsWith("Page", StringComparison.Ordinal) && className.Length > "Page".Length
            ? className.Substring(0, className.Length - "Page".Length)
            : className;
    }

    private static string ParameterName(PageRouteTemplate.Segment segment)
    {
        var name = segment.ParameterName!;
        var camelCase = char.ToLowerInvariant(name[0]) + name.Substring(1);
        return SyntaxFacts.GetKeywordKind(camelCase) == SyntaxKind.None ? camelCase : "@" + camelCase;
    }

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private sealed record RazorFile(string Path, string TargetPath, string? Namespace, string Routes)
    {
        public bool IsImports => TargetPath.EndsWith("_Imports.razor", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ClassPage(string FullName, string ClassName, string Routes, FileLinePositionSpan Location);

    private sealed class Page(string fullName, string className, string[] routes, Location location)
    {
        public string FullName { get; } = fullName;

        public string ClassName { get; } = className;

        public string[] Routes { get; } = routes;

        public Location Location { get; } = location;
    }
}
