using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Xunit;

namespace Watchious.Core.Tests;

public class BatchConfirmationTests
{
    [Fact]
    public async Task Tap_OnAButtonTheHostStillShows_ReachesItsHandler()
    {
        // Arrange
        var host = new DeferredHost();
        var state = new PageState { Items = ["a", "b"] };
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton(state);
        await using var app = builder.UseWatchiousApp<ClosureListPage>().Build();
        await app.StartAsync(host).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        host.ApplyPending();
        var shownId = host.Button("a").Events["click"];

        // A re-render gives the buttons new handlers, which the host hasn't shown yet.
        await app.DispatchEventAsync(host.Button("Refresh").Events["click"], "click", null);

        // Act
        await app.DispatchEventAsync(shownId, "click", null);

        // Assert
        state.Text.ShouldBe("a");
        host.ApplyPending();
        host.Button("a").Events["click"].ShouldNotBe(shownId);
        host.Errors.ShouldBeEmpty();
    }
}
