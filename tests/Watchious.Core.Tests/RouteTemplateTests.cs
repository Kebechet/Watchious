using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Navigation;
using Xunit;

namespace Watchious.Core.Tests;

// RouteTemplate is internal, so routes are tested through navigation, the way an app reaches them.
public class RouteTemplateTests
{
    [Theory]
    [InlineData("/detail/7", "Detail 7")]
    [InlineData("/DETAIL/7", "Detail 7")]
    [InlineData("/detail/7?from=list", "Detail 7")]
    [InlineData("/items/new", "New item")]
    [InlineData("/items/42", "Item 42")]
    [InlineData("/search", "Search: ")]
    [InlineData("/search/caf%C3%A9%20au%20lait", "Search: café au lait")]
    public async Task GoTo_MatchesTheRoute(string route, string expectedContent)
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        await app.Services.GetRequiredService<INavigation>().GoToAsync(route);

        // Assert
        host.LastTree.Single("NavigationStack").VisibleChildren.Last().Single("Label").Content.ShouldBe(expectedContent);
    }

    [Theory]
    [InlineData("/detail/seven")]
    [InlineData("/detail")]
    [InlineData("/detail/7/extra")]
    public async Task GoTo_RouteThatDoesNotMatch_Throws(string route)
    {
        // Arrange
        var (app, _) = await StartAsync();
        await using var disposeApp = app;
        var navigation = app.Services.GetRequiredService<INavigation>();

        // Act
        var goTo = () => navigation.GoToAsync(route);

        // Assert
        await goTo.ShouldThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("/detail/{Id:color}")]
    [InlineData("/{Name?}/detail")]
    [InlineData("/files/{*Path}")]
    public void AddRoute_UnsupportedTemplate_Throws(string template)
    {
        // Arrange
        var builder = WatchiousApp.CreateBuilder();

        // Act
        var addRoute = () => builder.AddRoute<DetailPage>(template);

        // Assert
        addRoute.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void RouteValue_FormatsTheWayRoutesParseThem()
    {
        // Arrange
        var date = new DateTime(2026, 11, 21, 3, 15, 0);

        // Act
        var values = new[]
        {
            RouteValue.Format(1.5),
            RouteValue.Format(true),
            RouteValue.Format("a/b c"),
            RouteValue.Format(date),
        };

        // Assert
        values.ShouldBe(["1.5", "true", "a%2Fb%20c", "2026-11-21T03%3A15%3A00.0000000"]);
    }

    [Fact]
    public async Task GoTo_LiteralRoute_WinsOverTheSameRouteWithAnOptionalParameter()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<NavigationRootPage>()
            .AddRoute<SearchPage>("/search/{Query?}")
            .AddRoute<NewItemPage>("/search")
            .Build();
        await app.StartAsync(host);
        var navigation = app.Services.GetRequiredService<INavigation>();

        // Act
        await navigation.GoToAsync("/search");
        var literal = host.LastTree.Single("NavigationStack").VisibleChildren.Last().Single("Label").Content;
        await navigation.GoToAsync("/search/tea");
        var optional = host.LastTree.Single("NavigationStack").VisibleChildren.Last().Single("Label").Content;

        // Assert
        literal.ShouldBe("New item");
        optional.ShouldBe("Search: tea");
    }

    [Fact]
    public async Task GoTo_DateTimeValue_ArrivesUnchanged()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<NavigationRootPage>()
            .AddRoute<DatePage>("/at/{When:datetime}")
            .Build();
        await app.StartAsync(host);
        var when = new DateTime(2026, 11, 21, 3, 15, 0, 250, DateTimeKind.Utc);

        // Act
        await app.Services.GetRequiredService<INavigation>().GoToAsync("/at/" + RouteValue.Format(when));

        // Assert
        host.LastTree.Single("NavigationStack").VisibleChildren.Last().Single("Label").Content
            .ShouldBe($"At {when.Ticks} Utc");
    }

    private static async Task<(WatchiousApp App, RecordingHost Host)> StartAsync()
    {
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<NavigationRootPage>()
            .AddRoute<DetailPage>("/detail/{Id:int}")
            .AddRoute<ItemPage>("/items/{Name}")
            .AddRoute<NewItemPage>("/items/new")
            .AddRoute<SearchPage>("/search/{Query?}")
            .Build();
        await app.StartAsync(host);
        return (app, host);
    }
}
