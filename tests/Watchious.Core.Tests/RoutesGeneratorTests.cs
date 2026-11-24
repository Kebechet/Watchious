using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    private static ImmutableArray<Diagnostic> Generate(string pages)
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
        var driver = CSharpGeneratorDriver.Create(new RoutesGenerator()).RunGenerators(compilation);
        return driver.GetRunResult().Diagnostics;
    }
}
