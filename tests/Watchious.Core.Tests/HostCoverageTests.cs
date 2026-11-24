using System.Text.RegularExpressions;
using Shouldly;
using Watchious.Controls;
using Xunit;

namespace Watchious.Core.Tests;

// The names the C# controls send are plain strings that each host matches by hand, so a control, attribute, event or
// value added to C# but not to a host would only show up as a missing or wrongly styled element
// (https://github.com/Kebechet/Watchious/issues/3). Each name only has to appear somewhere in a host's sources: which
// control reads it isn't checked.
public partial class HostCoverageTests
{
    // Read by the C# side only: tests find navigation pages by their route.
    private static readonly string[] CSharpOnlyAttributes = ["Route"];

    // Read by one watch host only, and by the preview, which draws both platforms.
    private static readonly Dictionary<string, string> PlatformOnlyAttributes = new()
    {
        [nameof(Image.AppleSymbol)] = "watchOS",
        [nameof(Image.MaterialIcon)] = "WearOS",
    };

    private static readonly string Root = FindRepositoryRoot();

    private static readonly Dictionary<string, string[]> HostSources = new()
    {
        ["watchOS"] = Directory.GetFiles(Path.Combine(Root, "platforms", "watchOS", "Host"), "*.swift"),
        ["WearOS"] = Directory.GetFiles(Path.Combine(Root, "platforms", "WearOS", "host", "src", "main", "kotlin"), "*.kt", SearchOption.AllDirectories),
        ["Preview"] = Directory.GetFiles(Path.Combine(Root, "src", "Watchious.Preview", "wwwroot"), "*.js"),
    };

    public static TheoryData<string> Hosts => [.. HostSources.Keys];

    // The preview maps text styles to CSS classes and draws every progress view alike, so it has no table of values.
    public static TheoryData<string> WatchHosts => ["watchOS", "WearOS"];

    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryControl_HasAViewInTheHost(string host)
    {
        // Arrange
        var source = HostSource(host);
        var controls = ControlsSource(OpenElement(), host);

        // Act
        var missing = controls.Where(control => !IsMatchedAsType(source, control)).ToList();

        // Assert
        controls.ShouldContain(nameof(Button));
        controls.ShouldContain("Page");
        missing.ShouldBeEmpty($"{host} has no view for these controls");
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void EveryAttribute_IsReadByTheHost(string host)
    {
        // Arrange
        var source = HostSource(host);
        var attributes = ControlsSource(AddAttribute(), host)
            .Except(CSharpOnlyAttributes)
            .Where(attribute => !PlatformOnlyAttributes.TryGetValue(attribute, out var platform) || platform == host || host == "Preview")
            .Select(attribute => attribute.StartsWith("on", StringComparison.Ordinal) ? attribute[2..] : attribute)
            .ToList();

        // Act
        var missing = attributes.Where(attribute => !source.Contains($"\"{attribute}\"", StringComparison.Ordinal)).ToList();

        // Assert
        attributes.ShouldContain(nameof(Label.Style));
        attributes.ShouldContain("click");
        missing.ShouldBeEmpty($"{host} doesn't read these attributes or events");
    }

    [Theory]
    [MemberData(nameof(WatchHosts))]
    public void EveryEnumValue_IsKnownToTheHost(string host)
    {
        // Arrange
        var source = HostSource(host);
        var values = typeof(Label).Assembly.GetExportedTypes()
            .Where(type => type.IsEnum && (PlatformOfNamespace(type.Namespace) is null || PlatformOfNamespace(type.Namespace) == host))
            .SelectMany(type => Enum.GetNames(type).Select(name => $"{type.Name}.{name}"))
            .ToList();

        // Act
        var missing = values.Where(value => !source.Contains($"\"{value.Split('.')[1]}\"", StringComparison.Ordinal)).ToList();

        // Assert
        values.ShouldContain($"{nameof(TextStyle)}.{nameof(TextStyle.Body)}");
        missing.ShouldBeEmpty($"{host} doesn't know these values");
    }

    private static string HostSource(string host)
    {
        return string.Join('\n', HostSources[host].Select(File.ReadAllText));
    }

    // Each control's element name and attribute names, as RenderTreeBuilder calls in Watchious.Controls write them,
    // that the host must know. The controls in Controls/Apple and Controls/Wear are for one platform: that watch host
    // and the preview must know their names, while the other host shows nothing for them.
    private static List<string> ControlsSource(Regex call, string host)
    {
        var files = Directory.GetFiles(Path.Combine(Root, "src", "Watchious.Controls"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        return files
            .SelectMany(file => call.Matches(File.ReadAllText(file)).Select(match => (Name: match.Groups["name"].Value, Platform: PlatformOfFile(file))))
            .GroupBy(found => found.Name)
            .Where(group => host == "Preview" || group.Any(found => found.Platform is null || found.Platform == host))
            .Select(group => group.Key)
            .ToList();
    }

    private static string? PlatformOfFile(string file)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(file));
        return PlatformOfNamespace(folder);
    }

    private static string? PlatformOfNamespace(string? name)
    {
        return name switch
        {
            "Apple" => "watchOS",
            "Wear" => "WearOS",
            _ => null,
        };
    }

    // A case of a switch on the node type in Swift and JavaScript, a branch of a Kotlin when, or a comparison.
    private static bool IsMatchedAsType(string source, string control)
    {
        var name = Regex.Escape($"\"{control}\"");
        return Regex.IsMatch(source, $@"case\s+(""[\w.]+""\s*,\s*)*{name}|{name}(\s*,\s*""[\w.]+"")*\s*->|==+\s*{name}");
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

    [GeneratedRegex(@"OpenElement\(\d+,\s*(nameof\((?<name>\w+)\)|""(?<name>[\w.]+)"")\)")]
    private static partial Regex OpenElement();

    [GeneratedRegex(@"AddAttribute\(\d+,\s*(nameof\((?<name>\w+)\)|""(?<name>\w+)"")")]
    private static partial Regex AddAttribute();
}
