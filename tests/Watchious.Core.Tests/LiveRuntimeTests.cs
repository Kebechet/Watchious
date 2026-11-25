using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Watchious.Core.Tests;

// The watchOS live host is the SwiftUI host compiled with LiveRuntime.swift in place of .NET, and it is built only on a
// developer's Mac, when live mode starts (dotnet build -t:WatchiousLive). A function the host gains in Watchious.h
// without one in LiveRuntime.swift would fail that link there, so the two are compared here.
public partial class LiveRuntimeTests
{
    [Fact]
    public void LiveRuntime_DefinesEveryFunctionTheHostDeclares()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var header = File.ReadAllText(Path.Combine(root, "platforms", "watchOS", "Host", "Watchious.h"));
        var liveRuntime = File.ReadAllText(Path.Combine(root, "platforms", "watchOS", "Live", "LiveRuntime.swift"));

        // Act
        var declared = HeaderFunction().Matches(header).Select(match => match.Groups["name"].Value).ToHashSet();
        var defined = CDeclFunction().Matches(liveRuntime).Select(match => match.Groups["name"].Value).ToHashSet();

        // Assert
        declared.ShouldNotBeEmpty();
        defined.ShouldBe(declared, ignoreOrder: true);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "platforms")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("The repository root wasn't found above the test output.");
    }

    // A declaration, not a callback type: typedef void (*watchious_text_callback)(...).
    [GeneratedRegex(@"^(?!\s*typedef)[^\n;(]*\b(?<name>watchious_\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex HeaderFunction();

    [GeneratedRegex(@"@_cdecl\(""(?<name>watchious_\w+)""\)")]
    private static partial Regex CDeclFunction();
}
