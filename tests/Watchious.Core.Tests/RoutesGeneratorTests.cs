using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Watchious.Navigation;
using Watchious.SourceGenerators;
using Xunit;

namespace Watchious.Core.Tests;

// The route generator's diagnostics, for pages declared with [Route], the attribute an @page directive becomes.
public class RoutesGeneratorTests
{
    [Fact]
    public void SameRoutes_AreAnError()
    {
        // Act
        var diagnostics = Generate("""
            [Route("/items/{Id:int}")] public class ItemPage : ComponentBase { }
            [Route("/Items/{Number:int}")] public class NumberPage : ComponentBase { }
            """);

        // Assert
        diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["WATCHIOUS004"]);
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    [Fact]
    public void RoutesWithOverlappingNumbers_AreAWarning()
    {
        // Act
        var diagnostics = Generate("""
            [Route("/items/{Id:int}")] public class ItemPage : ComponentBase { }
            [Route("/items/{Id:long}")] public class LongItemPage : ComponentBase { }
            """);

        // Assert
        diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["WATCHIOUS005"]);
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void RoutesThatPrecedenceTellsApart_AreFine()
    {
        // Act
        var diagnostics = Generate("""
            [Route("/items")] public class ItemsPage : ComponentBase { }
            [Route("/items/new")] public class NewItemPage : ComponentBase { }
            [Route("/items/{Id:int}")] public class ItemPage : ComponentBase { }
            [Route("/items/{Name}")] public class NamedItemPage : ComponentBase { }
            [Route("/items/{Id:guid}")] public class GuidItemPage : ComponentBase { }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    // From .NET 11 the Razor generator's declaration of each page, with its [Route], is in the compilation that other
    // generators see, so the generator finds a .razor page also as a class.
    [Fact]
    public void RazorPageAlsoDeclaredAsAClass_CountsOnce()
    {
        // Act
        var diagnostics = Generate(
            """[Route("/apis")] public partial class ApisPage : ComponentBase { }""",
            new RazorText("ApisPage.razor", "@page \"/apis\"\n<Label>APIs</Label>\n"));

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void RazorPageWithARouteInItsCodeBehind_GetsBothRoutes()
    {
        // Act
        var (diagnostics, generated) = GenerateWithSource(
            """[Route("/b/{Id:int}")] public partial class TwoRoutesPage : ComponentBase { }""",
            new RazorText("TwoRoutesPage.razor", "@page \"/a\"\n<Label>Two</Label>\n"));

        // Assert
        diagnostics.ShouldBeEmpty();
        generated.ShouldContain("builder.AddRoute<global::App.TwoRoutesPage>(\"/a\");");
        generated.ShouldContain("builder.AddRoute<global::App.TwoRoutesPage>(\"/b/{Id:int}\");");
    }

    private static ImmutableArray<Diagnostic> Generate(string pages, params AdditionalText[] razorFiles)
    {
        return GenerateWithSource(pages, razorFiles).Diagnostics;
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, string Generated) GenerateWithSource(string pages, params AdditionalText[] razorFiles)
    {
        var source = "using Microsoft.AspNetCore.Components;\nnamespace App;\n" + pages;
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(RouteAttribute).Assembly)
            .Append(typeof(IWatchiousRoutes).Assembly)
            .Where(assembly => !assembly.IsDynamic && assembly.Location.Length > 0)
            .Distinct()
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create(
            "App",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(
                [new RoutesGenerator().AsSourceGenerator()],
                razorFiles,
                optionsProvider: new RootNamespaceOptions("App"))
            .RunGenerators(compilation);
        var result = driver.GetRunResult();
        return (result.Diagnostics, string.Join("\n", result.GeneratedTrees.Select(tree => tree.ToString())));
    }

    private sealed class RazorText(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(text);
        }
    }

    private sealed class RootNamespaceOptions(string rootNamespace) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(new() { ["build_property.RootNamespace"] = rootNamespace });

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return new Options([]);
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return new Options([]);
        }

        private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                return values.TryGetValue(key, out value);
            }
        }
    }
}
