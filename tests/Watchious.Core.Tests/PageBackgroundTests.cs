using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Controls;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Xunit;

namespace Watchious.Core.Tests;

public class PageBackgroundTests
{
    [Fact]
    public async Task Start_PageWithItsOwnColor_SendsItInTheFirstBatch()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<ColoredPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        host.Batches.Count.ShouldBe(1);
        Pages(host).ShouldHaveSingleItem().Attribute("Background").ShouldBe("#512BD4FF");
    }

    [Fact]
    public async Task Start_PageWithoutAColor_GetsTheThemes()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<StaticPage>()
            .ConfigureTheme(theme => theme.PageBackgroundColor = Colors.Navy)
            .Build();

        // Act
        await app.StartAsync(host);

        // Assert
        Pages(host).ShouldHaveSingleItem().Attribute("Background").ShouldBe("#000080FF");
    }

    [Fact]
    public async Task Start_NoColorAnywhere_SendsNoBackground()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>().Build();

        // Act
        await app.StartAsync(host);

        // Assert
        Pages(host).ShouldHaveSingleItem().Attributes.ShouldNotContainKey("Background");
    }

    [Fact]
    public async Task Start_ThemeBrush_WinsOverTheThemeColor()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<StaticPage>()
            .ConfigureTheme(theme =>
            {
                theme.PageBackgroundColor = Colors.Navy;
                theme.PageBackground = new LinearGradientBrush(
                    [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)], new Point(0, 0), new Point(0, 1));
            })
            .Build();

        // Act
        await app.StartAsync(host);

        // Assert
        Pages(host).ShouldHaveSingleItem().Attribute("Background").ShouldBe("linear 0 0 0 1 #FF0000FF 0 #0000FFFF 1");
    }

    [Fact]
    public async Task PageBrush_WinsOverItsColorAndTheTheme()
    {
        // Arrange
        var (app, host) = await StartAsync(theme => theme.PageBackgroundColor = Colors.Navy);
        await using var disposeApp = app;
        await ClickAsync(app, host, "Open colored");

        // Act
        await ClickAsync(app, host, "Gradient");

        // Assert
        Pages(host)[1].Attribute("Background").ShouldBe("radial 0.5 0.5 0.5 #FFFFFFFF 0 #000000FF 1");
    }

    [Fact]
    public async Task PageWithoutItsOwnValues_FallsBackToTheTheme()
    {
        // Arrange
        var (app, host) = await StartAsync(theme => theme.PageBackgroundColor = Colors.Navy);
        await using var disposeApp = app;
        await ClickAsync(app, host, "Open colored");

        // Act
        await ClickAsync(app, host, "Theme");

        // Assert
        Pages(host)[1].Attribute("Background").ShouldBe("#000080FF");
    }

    [Fact]
    public async Task ThemeChange_UpdatesThePagesWithoutTheirOwn()
    {
        // Arrange
        var (app, host) = await StartAsync(theme => theme.PageBackgroundColor = Colors.Navy);
        await using var disposeApp = app;
        await ClickAsync(app, host, "Open colored");

        // Act
        app.Services.GetRequiredService<WatchiousTheme>().PageBackgroundColor = Colors.Red;
        await WaitForBatchAsync();

        // Assert
        var pages = Pages(host);
        pages[0].Attribute("Background").ShouldBe("#FF0000FF");
        pages[1].Attribute("Background").ShouldBe("#512BD4FF");
    }

    [Fact]
    public async Task CustomPage_IsReadThroughIPage()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        await ClickAsync(app, host, "Open custom");

        // Act
        CustomBackgroundPage.LastCreated!.ChangeColor(Colors.Orange);
        await WaitForBatchAsync();

        // Assert
        Pages(host)[1].Attribute("Background").ShouldBe("#FFA500FF");
    }

    [Fact]
    public async Task ClosingAPage_StopsFollowingIt()
    {
        // Arrange
        var (app, host) = await StartAsync();
        await using var disposeApp = app;
        await ClickAsync(app, host, "Open custom");
        var page = CustomBackgroundPage.LastCreated!;
        page.HasSubscribers.ShouldBeTrue();
        await app.Services.GetRequiredService<Watchious.Navigation.INavigation>().GoBackAsync();
        var batches = host.Batches.Count;

        // Act
        page.ChangeColor(Colors.Orange);
        await WaitForBatchAsync();

        // Assert
        page.HasSubscribers.ShouldBeFalse();
        host.Batches.Count.ShouldBe(batches);
    }

    [Fact]
    public async Task Dispose_StopsFollowingThePagesAndTheTheme()
    {
        // Arrange
        var theme = new CountingTheme();
        var host = new RecordingHost();
        var builder = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<BackgroundRootPage>()
            .AddRoute<ColoredPage>("/colored")
            .AddRoute<CustomBackgroundPage>("/custom-background");
        builder.Services.AddSingleton<ITheme>(theme);
        var app = builder.Build();
        await app.StartAsync(host);
        await ClickAsync(app, host, "Open custom");
        var page = CustomBackgroundPage.LastCreated!;
        theme.HasSubscribers.ShouldBeTrue();
        page.HasSubscribers.ShouldBeTrue();

        // Act
        await app.DisposeAsync();
        theme.ChangeColor(Colors.Red);
        page.ChangeColor(Colors.Orange);

        // Assert
        theme.HasSubscribers.ShouldBeFalse();
        page.HasSubscribers.ShouldBeFalse();
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task CustomTheme_RegisteredInServices_IsTheOneRead()
    {
        // Arrange
        var theme = new CountingTheme();
        var host = new RecordingHost();
        var builder = WatchiousApp.CreateBuilder().UseWatchiousApp<StaticPage>();
        builder.Services.AddSingleton<ITheme>(theme);
        await using var app = builder.Build();
        await app.StartAsync(host);

        // Act
        theme.ChangeColor(Colors.Red);
        await WaitForBatchAsync();

        // Assert
        Pages(host).ShouldHaveSingleItem().Attribute("Background").ShouldBe("#FF0000FF");
    }

    [Fact]
    public async Task PageUsedInsideAnotherPage_DoesNotSetItsBackground()
    {
        // Arrange and act
        var (app, host) = await StartAsync();
        await using var disposeApp = app;

        // Assert
        var root = Pages(host).ShouldHaveSingleItem();
        root.Descendants().ShouldContain(node => node.Type == "Label" && node.Content == "Colored");
        root.Attributes.ShouldNotContainKey("Background");
    }

    [Theory]
    [InlineData("#512BD4", "#FF512BD4")]
    [InlineData("512BD4", "#FF512BD4")]
    [InlineData("#80512BD4", "#80512BD4")]
    [InlineData("#f0a", "#FFFF00AA")]
    [InlineData("#8f0a", "#88FF00AA")]
    public void FromArgb_ReadsMauisHexForms(string hex, string argb)
    {
        // Act
        var color = Color.FromArgb(hex);

        // Assert
        color.ToArgbHex().ShouldBe(argb);
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    [InlineData("")]
    public void FromArgb_NotAColor_Throws(string hex)
    {
        // Act
        var fromArgb = () => Color.FromArgb(hex);

        // Assert
        fromArgb.ShouldThrow<FormatException>();
    }

    [Fact]
    public void Colors_HaveMauisNamesAndValues()
    {
        // Assert
        Colors.RebeccaPurple.ToArgbHex().ShouldBe("#FF663399");
        Colors.Transparent.Alpha.ShouldBe(0);
        Colors.Red.ShouldBe(Color.FromRgb(255, 0, 0));
    }

    [Fact]
    public void Gradients_WithOneOrNoStops_AreAColorOrNothing()
    {
        // Arrange
        var oneStop = new LinearGradientBrush([new GradientStop(Colors.Red, 0.3f)]);
        var noStops = new RadialGradientBrush([]);

        // Act and assert
        PageBackground.Resolve(new BrushPage(oneStop), new WatchiousTheme()).ShouldBe("#FF0000FF");
        PageBackground.Resolve(new BrushPage(noStops), new WatchiousTheme()).ShouldBeNull();
    }

    [Fact]
    public void Gradient_SortsItsStopsAndWritesNumbersTheSameInEveryCulture()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
        try
        {
            var brush = new LinearGradientBrush(
                [new GradientStop(Colors.Blue, 1), new GradientStop(Colors.Red, 0.25f)], new Point(0.5, 0), new Point(0.5, 1));

            // Act
            var background = PageBackground.Resolve(new BrushPage(brush), new WatchiousTheme());

            // Assert
            background.ShouldBe("linear 0.5 0 0.5 1 #FF0000FF 0.25 #0000FFFF 1");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static async Task<(WatchiousApp App, RecordingHost Host)> StartAsync(Action<WatchiousTheme>? configureTheme = null)
    {
        var host = new RecordingHost();
        var app = WatchiousApp.CreateBuilder()
            .UseWatchiousApp<BackgroundRootPage>()
            .AddRoute<ColoredPage>("/colored")
            .AddRoute<CustomBackgroundPage>("/custom-background")
            .ConfigureTheme(configureTheme ?? (_ => { }))
            .Build();
        await app.StartAsync(host);
        host.Errors.ShouldBeEmpty();
        return (app, host);
    }

    private static Task ClickAsync(WatchiousApp app, RecordingHost host, string buttonText)
    {
        var button = host.LastTree.Descendants().Last(node => node.Type == "Button" && node.Content == buttonText);
        return app.DispatchEventAsync(button.Events["click"], "click", null);
    }

    // A change outside an event (the theme, a page's own method) renders through the renderer's dispatcher.
    private static Task WaitForBatchAsync()
    {
        return Task.Delay(100);
    }

    private static List<UiNode> Pages(RecordingHost host)
    {
        return [.. host.LastTree.Single("NavigationStack").VisibleChildren];
    }

    private sealed class BrushPage(Brush brush) : IPage
    {
#pragma warning disable CS0067 // Never raised: the tests only read the page.
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        public Color? BackgroundColor => null;

        public Brush? Background => brush;
    }
}
