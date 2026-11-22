using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Xunit;

namespace Watchious.Core.Tests;

public class ControlsTests
{
    [Fact]
    public async Task List_SendsItsRowsAsChildren()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var list = host.LastTree.Single("List");

        // Assert
        list.VisibleChildren.ShouldHaveSingleItem().Content.ShouldBe("Row");
    }

    [Fact]
    public async Task ProgressView_ClampsTheValueAndLeavesItOutWhenIndeterminate()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var progressViews = host.LastTree.Descendants().Where(node => node.Type == "ProgressView").ToList();

        // Assert
        progressViews[0].Attribute("Value").ShouldBe("1");
        progressViews[0].Attribute("Text").ShouldBe("Upload");
        progressViews[0].Attribute("Style").ShouldBe("Automatic");
        progressViews[1].Attributes.ShouldNotContainKey("Value");
    }

    [Fact]
    public async Task Picker_SendsItemsOnSeparateLines()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var picker = host.LastTree.Single("Picker");

        // Assert
        picker.Attribute("Items").ShouldBe("a\nb\nc d");
        picker.Attribute("SelectedIndex").ShouldBe("1");
        picker.Attribute("Text").ShouldBe("Reps");
    }

    [Fact]
    public async Task Picker_Selected_UpdatesTheBoundIndex()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var picker = host.LastTree.Single("Picker");

        // Act
        await app.DispatchEventAsync(picker.Events["selected"], "selected", "2");

        // Assert
        picker.Attribute("SelectedIndex").ShouldBe("2");
        host.LastTree.Descendants().ShouldContain(node => node.Content == "Selected: c\nd");
    }

    [Theory]
    [InlineData("3")]
    [InlineData("-1")]
    [InlineData("two")]
    public async Task Picker_SelectedOutOfRange_IsIgnored(string index)
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var picker = host.LastTree.Single("Picker");

        // Act
        await app.DispatchEventAsync(picker.Events["selected"], "selected", index);

        // Assert
        picker.Attribute("SelectedIndex").ShouldBe("1");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task TextField_Changed_UpdatesTheBoundText()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        var textField = host.LastTree.Single("TextField");
        textField.Attribute("Text").ShouldBe("");
        textField.Attribute("Placeholder").ShouldBe("Name");

        // Act
        await app.DispatchEventAsync(textField.Events["changed"], "changed", "Ada");

        // Assert
        textField.Attribute("Text").ShouldBe("Ada");
        host.LastTree.Descendants().ShouldContain(node => node.Content == "Name: Ada");
    }

    [Fact]
    public async Task Image_SendsItsSourceAndOnlyTheSizesThatAreSet()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Act
        var image = host.LastTree.Single("Image");

        // Assert
        image.Attribute("Source").ShouldBe("logo");
        image.Attribute("Width").ShouldBe("32");
        image.Attributes.ShouldNotContainKey("Height");
        image.Attribute("Description").ShouldBe("Logo");
    }

    private static async Task<(WatchiousApp App, RecordingHost Host)> StartAsync()
    {
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder().UseWatchiousApp<ControlsPage>().Build();
        await app.StartAsync(host);
        host.Errors.ShouldBeEmpty();
        return (app, host);
    }
}
