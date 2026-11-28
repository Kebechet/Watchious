using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Navigation;
using Xunit;

namespace Watchious.Core.Tests;

public class NavigationTests
{
    [Fact]
    public async Task Start_ShowsTheRootPageInANavigationStack()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var stack = host.LastTree.VisibleChildren.ShouldHaveSingleItem();

        // Assert
        stack.Type.ShouldBe("NavigationStack");
        var page = stack.VisibleChildren.ShouldHaveSingleItem();
        page.Type.ShouldBe("Page");
        page.Attribute("Route").ShouldBe("/");
        app.Services.GetRequiredService<INavigation>().Stack.ShouldBe(["/"]);
    }

    [Fact]
    public async Task GoTo_PushesThePageWithItsRouteParameters()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        await ClickAsync(app, host, "Open");

        // Assert
        var pages = Pages(host);
        pages.Select(page => page.Attribute("Route")).ShouldBe(["/", "/detail/7"]);
        pages[1].Single("Label").Content.ShouldBe("Detail 7");
        var navigation = app.Services.GetRequiredService<INavigation>();
        navigation.Stack.ShouldBe(["/", "/detail/7"]);
        navigation.CanGoBack.ShouldBeTrue();
    }

    [Fact]
    public async Task GoBack_ShowsThePreviousPageWithItsState()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        await ClickAsync(app, host, "+1");
        await ClickAsync(app, host, "+1");
        var rootPage = Pages(host)[0];
        var rootNodeIds = rootPage.Descendants().Select(node => node.Id).ToList();
        await ClickAsync(app, host, "Open");

        // Act
        await app.Services.GetRequiredService<INavigation>().GoBackAsync();

        // Assert
        Pages(host).ShouldHaveSingleItem().ShouldBeSameAs(rootPage);
        rootPage.Single("Label").Content.ShouldBe("Count: 2");
        rootPage.Descendants().Select(node => node.Id).ShouldBe(rootNodeIds);
    }

    [Fact]
    public async Task NativeBack_PopsToThePageCountTheHostReports()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var navigation = app.Services.GetRequiredService<INavigation>();
        await navigation.GoToAsync("/detail/1");
        await navigation.GoToAsync("/detail/2");
        var backHandlerId = host.LastTree.Single("NavigationStack").Events["back"];

        // Act
        await app.DispatchEventAsync(backHandlerId, "back", "1");

        // Assert
        Pages(host).Select(page => page.Attribute("Route")).ShouldBe(["/"]);
        navigation.Stack.ShouldBe(["/"]);
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task GoBackToRoot_LeavesOnlyTheRootPage()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var navigation = app.Services.GetRequiredService<INavigation>();
        await navigation.GoToAsync("/detail/1");
        await navigation.GoToAsync("/detail/2");

        // Act
        await navigation.GoBackToRootAsync();

        // Assert
        Pages(host).Select(page => page.Attribute("Route")).ShouldBe(["/"]);
        navigation.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public async Task GoBack_OnTheRootPage_DoesNothing()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        await app.Services.GetRequiredService<INavigation>().GoBackAsync();

        // Assert
        Pages(host).ShouldHaveSingleItem();
        host.Batches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GoTo_UnknownRoute_Throws()
    {
        // Arrange
        var (app, _) = await StartAsync();
        await using var disposeApp = app;
        var navigation = app.Services.GetRequiredService<INavigation>();

        // Act
        var goToUnknown = () => navigation.GoToAsync("/nowhere");

        // Assert
        await goToUnknown.ShouldThrowAsync<ArgumentException>();
        navigation.Stack.ShouldBe(["/"]);
    }

    [Fact]
    public async Task Dispose_UnsubscribesTheRootFromNavigationChanges()
    {
        // Arrange
        var (app, host) = await StartAsync();
        var navigation = (NavigationService)app.Services.GetRequiredService<INavigation>();
        navigation.HasChangedSubscribers.ShouldBeTrue();

        // Act
        await app.DisposeAsync();
        await navigation.GoToAsync("/detail/3");

        // Assert
        navigation.HasChangedSubscribers.ShouldBeFalse();
        host.Batches.Count.ShouldBe(1);
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task DoubleTap_OnAButtonThatOpensAPage_OpensItOnce()
    {
        // Arrange: both taps come from the button the host showed before the page opened.
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var openHandlerId = Button(host, "Open").Events["click"];

        // Act
        await app.DispatchEventAsync(openHandlerId, "click", null);
        await app.DispatchEventAsync(openHandlerId, "click", null);

        // Assert
        app.Services.GetRequiredService<INavigation>().Stack.ShouldBe(["/", "/detail/7"]);
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Tap_OnAPageThatAnotherCovers_IsIgnored()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var addHandlerId = Button(host, "+1").Events["click"];
        await ClickAsync(app, host, "Open");

        // Act
        await app.DispatchEventAsync(addHandlerId, "click", null);
        await app.Services.GetRequiredService<INavigation>().GoBackAsync();

        // Assert
        Pages(host).ShouldHaveSingleItem().Single("Label").Content.ShouldBe("Count: 0");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task Tap_OnThePageBackOnTop_Works()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        await ClickAsync(app, host, "Open");
        await app.Services.GetRequiredService<INavigation>().GoBackAsync();

        // Act
        await ClickAsync(app, host, "+1");
        await ClickAsync(app, host, "Open");

        // Assert
        Pages(host)[0].Single("Label").Content.ShouldBe("Count: 1");
        app.Services.GetRequiredService<INavigation>().Stack.ShouldBe(["/", "/detail/7"]);
        host.Errors.ShouldBeEmpty();
    }

    private static async Task<(WatchiousApp App, RecordingHost Host)> StartAsync()
    {
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<NavigationRootPage>()
            .AddRoute<NavigationRootPage>("/")
            .AddRoute<DetailPage>("/detail/{Id:int}")
            .Build();
        await app.StartAsync(host);
        host.Errors.ShouldBeEmpty();
        return (app, host);
    }

    private static Task ClickAsync(WatchiousApp app, RecordingHost host, string buttonText)
    {
        return app.DispatchEventAsync(Button(host, buttonText).Events["click"], "click", null);
    }

    private static UiNode Button(RecordingHost host, string text)
    {
        return host.LastTree.Descendants().Last(node => node.Type == "Button" && node.Content == text);
    }

    private static List<UiNode> Pages(RecordingHost host)
    {
        return [.. host.LastTree.Single("NavigationStack").VisibleChildren];
    }
}
