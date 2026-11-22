using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Navigation;
using Xunit;

namespace Watchious.Core.Tests;

// The test pages' @page directives go through Watchious's source generator like an app's: these tests compile only
// if it generated the Routes helpers.
public class GeneratedRoutesTests
{
    [Fact]
    public void Routes_BuildTheRouteFromTypedParameters()
    {
        // Act
        var routes = new[]
        {
            Routes.NavigationRoot(),
            Routes.Detail(id: 7),
            Routes.Search(),
            Routes.Search(query: "café au lait"),
        };

        // Assert
        routes.ShouldBe(["/", "/detail/7", "/search", "/search/caf%C3%A9%20au%20lait"]);
    }

    [Fact]
    public async Task GoTo_GeneratedRoute_NeedsNoRouteRegisteredByHand()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<NavigationRootPage>().Build();
        await app.StartAsync(host);

        // Act
        await app.Services.GetRequiredService<INavigation>().GoToAsync(Routes.Search(query: "café au lait"));

        // Assert
        host.LastTree.Single("NavigationStack").VisibleChildren.Last().Single("Label").Content.ShouldBe("Search: café au lait");
    }

    [Fact]
    public async Task Start_RootPageWithAPageRoute_StartsWithThatRoute()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<NavigationRootPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        app.Services.GetRequiredService<INavigation>().Stack.ShouldBe([Routes.NavigationRoot()]);
    }
}
