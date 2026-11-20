using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

public class WatchiousRendererTests
{
    [Fact]
    public async Task Start_RendersElementsWithAttributesAsNodeTree()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        var stack = host.LastTree.Children.ShouldHaveSingleItem();
        stack.Type.ShouldBe("VerticalStackLayout");
        stack.Attribute("Spacing").ShouldBe("8");
        stack.Children.Select(child => child.Type).ShouldBe(["Label", "Button"]);
        stack.Children[0].Attribute("Text").ShouldBe("Hello");
        stack.Children[0].Attribute("Style").ShouldBe("Title");
    }

    [Fact]
    public async Task Start_SendsFalseBooleansExplicitly()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        var button = host.LastTree.Single("Button");
        button.Attribute("IsEnabled").ShouldBe("false");
        button.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Start_FormatsNumbersCultureInvariant()
    {
        // Arrange
        var host = new RecordingHost();
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sk-SK");
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<FractionalSpacingPage>().Build();

        try
        {
            // Act
            await app.StartAsync(host);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        // Assert
        host.LastTree.Single("VerticalStackLayout").Attribute("Spacing").ShouldBe("8.5");
    }

    [Fact]
    public async Task Start_FlattensComponentsIntoTheirParentElement()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<NestedPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        var stack = host.LastTree.Single("VerticalStackLayout");
        stack.Children.Select(child => child.Type).ShouldBe(["Label", "Label", "Label"]);
        stack.Children.Select(child => child.Attribute("Text")).ShouldBe(["Ann", "Bob", "Cid"]);
    }

    [Fact]
    public async Task Start_InjectsServicesFromTheAppBuilder()
    {
        // Arrange
        var host = new RecordingHost();
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton<GreetingService>();
        await using var app = builder.UseWatchiousApp<InjectedPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        host.LastTree.Single("Label").Attribute("Text").ShouldBe("Hello from DI");
    }

    [Fact]
    public async Task DispatchEvent_ButtonClick_RerendersThePage()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        await app.StartAsync(host);
        var clickHandlerId = host.LastTree.Single("Button").Events["click"];

        // Act
        await app.DispatchEventAsync(clickHandlerId, "click", null);

        // Assert
        host.Trees.Count.ShouldBe(2);
        host.LastTree.Descendants().ShouldContain(node => node.Type == "Label" && node.Attribute("Text") == "Count: 1");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchEvent_SwitchToggled_UpdatesTheBoundParentState()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        await app.StartAsync(host);
        var toggledHandlerId = host.LastTree.Single("Switch").Events["toggled"];

        // Act
        await app.DispatchEventAsync(toggledHandlerId, "toggled", "true");

        // Assert
        var tree = host.LastTree;
        tree.Single("Switch").Attribute("IsToggled").ShouldBe("true");
        tree.Descendants().ShouldContain(node => node.Type == "Label" && node.Attribute("Text") == "Warm-up set");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Start_CalledTwice_Throws()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>().Build();
        await app.StartAsync(host);

        // Act
        var secondStart = () => app.StartAsync(host);

        // Assert
        await secondStart.ShouldThrowAsync<InvalidOperationException>();
    }
}
