using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Navigation;
using Xunit;

namespace Watchious.Core.Tests;

// Taps the host sends from what it shows before a batch reaches its screen, with a host that confirms batches later, as
// the SwiftUI and Compose hosts do: replaced handler ids keep working until then.
public class StaleTapTests
{
    [Fact]
    public async Task DoubleTap_OnARowThatOpensAPage_OpensItOnce()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var rowHandlerId = host.Button("Item 7").Events["click"];

        // Act
        await app.DispatchEventAsync(rowHandlerId, "click", null);
        await app.DispatchEventAsync(rowHandlerId, "click", null);
        host.ApplyPending();

        // Assert
        Stack(app).ShouldBe(["/", "/row/7"]);
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task TapOnASecondRow_BeforeTheFirstPageShows_IsIgnored()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var firstHandlerId = host.Button("Item 7").Events["click"];
        var secondHandlerId = host.Button("Item 8").Events["click"];

        // Act
        await app.DispatchEventAsync(firstHandlerId, "click", null);
        await app.DispatchEventAsync(secondHandlerId, "click", null);
        host.ApplyPending();

        // Assert
        Stack(app).ShouldBe(["/", "/row/7"]);
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task NativeBack_ThenATapOnTheRevealedPage_Works()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var addHandlerId = host.Button("+1").Events["click"];
        await app.DispatchEventAsync(host.Button("Item 7").Events["click"], "click", null);
        host.ApplyPending();
        var backHandlerId = host.Tree.Root.Single("NavigationStack").Events["back"];

        // Act
        await app.DispatchEventAsync(backHandlerId, "back", "1");
        await app.DispatchEventAsync(addHandlerId, "click", null);
        host.ApplyPending();

        // Assert
        Stack(app).ShouldBe(["/"]);
        host.Tree.Root.Descendants().Single(node => node.Type == "Label").Content.ShouldBe("Count: 1");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task DoubleTap_OnBack_GoesBackOnce()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var navigation = app.Services.GetRequiredService<INavigation>();
        await navigation.GoToAsync("/row/7");
        await navigation.GoToAsync("/row/8");
        host.ApplyPending();
        var backHandlerId = host.Tree.Root.Descendants().Last(node => node.Type == "Button" && node.Content == "Back").Events["click"];

        // Act
        await app.DispatchEventAsync(backHandlerId, "click", null);
        await app.DispatchEventAsync(backHandlerId, "click", null);
        host.ApplyPending();

        // Assert
        Stack(app).ShouldBe(["/", "/row/7"]);
        host.Errors.ShouldBeEmpty();
    }

    private static async Task<(WatchiousApp App, DeferredHost Host)> StartAsync()
    {
        var host = new DeferredHost();
        var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<RowsRootPage>()
            .AddRoute<RowDetailPage>("/row/{Id:int}")
            .Build();
        await app.StartAsync(host);
        host.ApplyPending();
        host.Errors.ShouldBeEmpty();
        return (app, host);
    }

    private static IReadOnlyList<string> Stack(WatchiousApp app)
    {
        return app.Services.GetRequiredService<INavigation>().Stack;
    }
}
