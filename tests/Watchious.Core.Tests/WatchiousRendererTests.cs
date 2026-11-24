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
        var stack = host.LastTree.Single("VStack");
        stack.Attribute("Spacing").ShouldBe("8");
        stack.VisibleChildren.Select(child => child.Type).ShouldBe(["Label", "Button"]);
        var title = stack.VisibleChildren.First();
        title.Content.ShouldBe("Hello");
        title.Attribute("Style").ShouldBe("Title");
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
        host.LastTree.Single("VStack").Attribute("Spacing").ShouldBe("8.5");
    }

    [Fact]
    public async Task Start_StackWithoutSpacing_LeavesItToThePlatform()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<NestedPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        var stack = host.LastTree.Single("VStack");
        stack.Attributes.ShouldNotContainKey("Spacing");
        stack.Attribute("Alignment").ShouldBe("Center");
    }

    [Fact]
    public async Task Start_SendsTheToggleLabel()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();

        // Act
        await app.StartAsync(host);

        // Assert
        host.LastTree.Single("Toggle").Attribute("Text").ShouldBe("Warm-up");
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
        var stack = host.LastTree.Single("VStack");
        stack.VisibleChildren.Select(child => child.Type).ShouldBe(["Label", "Label", "Label"]);
        stack.VisibleChildren.Select(child => child.Content).ShouldBe(["Ann", "Bob", "Cid"]);
    }

    [Fact]
    public async Task Start_LabelKeepsTheSpaceBetweenTwoValues()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<TwoValuesPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        host.LastTree.Single("Label").Content.ShouldBe("Ada Lovelace");
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
        host.LastTree.Single("Label").Content.ShouldBe("Hello from DI");
    }

    [Fact]
    public async Task DispatchEvent_ButtonClick_RerendersThePage()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        await app.StartAsync(host);
        var clickHandlerId = host.Button("+1").Events["click"];

        // Act
        await app.DispatchEventAsync(clickHandlerId, "click", null);

        // Assert
        host.Batches.Count.ShouldBe(2);
        host.LastTree.Descendants().ShouldContain(node => node.Type == "Label" && node.Content == "Count: 1");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchEvent_ToggleToggled_StaysAsThePageSaysWhenItRefuses()
    {
        // Arrange
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder().UseWatchiousApp<RefusingTogglePage>().Build();
        await using var disposeApp = app;
        await app.StartAsync(host);
        var toggledHandlerId = host.LastTree.Single("Toggle").Events["toggled"];

        // Act
        await app.DispatchEventAsync(toggledHandlerId, "toggled", "true");

        // Assert
        host.LastTree.Descendants().ShouldContain(node => node.Content == "Tries: 1");
        host.LastTree.Single("Toggle").Attribute("IsOn").ShouldBe("false");
    }

    [Fact]
    public async Task DispatchEvent_ToggleToggled_UpdatesTheBoundParentState()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        await app.StartAsync(host);
        var toggledHandlerId = host.LastTree.Single("Toggle").Events["toggled"];

        // Act
        await app.DispatchEventAsync(toggledHandlerId, "toggled", "true");

        // Assert
        var tree = host.LastTree;
        tree.Single("Toggle").Attribute("IsOn").ShouldBe("true");
        tree.Descendants().ShouldContain(node => node.Type == "Label" && node.Content == "Warm-up set");
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
