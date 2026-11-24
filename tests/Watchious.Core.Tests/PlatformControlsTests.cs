using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Xunit;

namespace Watchious.Core.Tests;

public class PlatformControlsTests
{
    [Fact]
    public async Task Gauge_SendsItsRangeLabelsAndStyle_AndKeepsTheValueInTheRange()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var gauge = host.LastTree.Single("Apple.Gauge");

        // Assert
        gauge.Attribute("Value").ShouldBe("180");
        gauge.Attribute("From").ShouldBe("40");
        gauge.Attribute("Through").ShouldBe("180");
        gauge.Attribute("Text").ShouldBe("Heart rate");
        gauge.Attribute("CurrentValueText").ShouldBe("250");
        gauge.Attribute("MinimumText").ShouldBe("40");
        gauge.Attribute("MaximumText").ShouldBe("180");
        gauge.Attribute("Style").ShouldBe("Circular");
    }

    [Fact]
    public async Task Gauge_SendsANaNOrTooLowValueAsFrom()
    {
        // Arrange
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder().UseWatchiousApp<ClampedGaugesPage>().Build();
        await using var disposeApp = app;

        // Act
        await app.StartAsync(host);

        // Assert
        host.Errors.ShouldBeEmpty();
        host.LastTree.Descendants().Where(node => node.Type == "Apple.Gauge").Select(node => node.Attribute("Value")).ShouldBe(["10", "10"]);
    }

    [Fact]
    public async Task Gauge_ThroughBelowFrom_ReportsTheError()
    {
        // Arrange
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder().UseWatchiousApp<BackwardsGaugePage>().Build();
        await using var disposeApp = app;

        // Act
        await app.StartAsync(host);

        // Assert
        host.Errors.ShouldHaveSingleItem().ShouldBeOfType<ArgumentOutOfRangeException>().ParamName.ShouldBe("Through");
    }

    [Fact]
    public async Task WearControls_SendTheirTexts()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var timeText = host.LastTree.Single("Wear.TimeText");
        var curvedText = host.LastTree.Single("Wear.CurvedText");
        var edgeButton = host.LastTree.Single("Wear.EdgeButton");

        // Assert
        timeText.Attribute("Text").ShouldBe("Run");
        curvedText.Attribute("Text").ShouldBe("Swipe");
        edgeButton.Attribute("Text").ShouldBe("Add");
        edgeButton.Attribute("Icon").ShouldBe("Plus");
        edgeButton.Attribute("IsEnabled").ShouldBe("true");
    }

    [Fact]
    public async Task EdgeButton_Click_RunsOnClick()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var edgeButton = host.LastTree.Single("Wear.EdgeButton");

        // Act
        await app.DispatchEventAsync(edgeButton.Events["click"], "click", null);

        // Assert
        host.LastTree.Descendants().ShouldContain(node => node.Content == "Taps: 1");
    }

    private static async Task<(WatchiousApp App, RecordingHost Host)> StartAsync()
    {
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder().UseWatchiousApp<PlatformControlsPage>().Build();
        await app.StartAsync(host);
        host.Errors.ShouldBeEmpty();
        return (app, host);
    }
}
