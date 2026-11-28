using Shouldly;
using SkiaSharp;
using Watchious.AppIcon;
using Xunit;

namespace Watchious.Core.Tests;

public sealed class AppIconTests : IDisposable
{
    private static readonly SKColor Navy = new(0x17, 0x12, 0x2B);
    private static readonly SKColor Lavender = new(0xB9, 0xA6, 0xFF);

    private readonly string _folder = Directory.CreateTempSubdirectory("watchious-appicon-").FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Theory]
    [InlineData("65%", 0.65f)]
    [InlineData(" 100% ", 1f)]
    [InlineData("1%", 0.01f)]
    [InlineData("72.5%", 0.725f)]
    public void ParseSize_ReadsAPercentage(string text, float size)
    {
        // Act
        var parsed = AppIconSettings.ParseSize(text);

        // Assert
        parsed.ShouldBe(size, 0.0001f);
    }

    [Theory]
    [InlineData("65")]
    [InlineData("0.65")]
    [InlineData("0%")]
    [InlineData("120%")]
    [InlineData("big%")]
    public void ParseSize_NotAPercentageFrom1To100_Throws(string text)
    {
        // Act
        Action parse = () => AppIconSettings.ParseSize(text);

        // Assert
        parse.ShouldThrow<AppIconException>().Message.ShouldContain("ForegroundSize");
    }

    [Theory]
    [InlineData("#512BD4", 0xFF512BD4u)]
    [InlineData("512BD4", 0xFF512BD4u)]
    [InlineData("#80FF0000", 0x80FF0000u)]
    [InlineData("#F0A", 0xFFFF00AAu)]
    public void ParseColor_ReadsMauisHexForms(string text, uint argb)
    {
        // Act
        var color = AppIconSettings.ParseColor(text, "BackgroundColor");

        // Assert
        ((uint)color).ShouldBe(argb);
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    public void ParseColor_NotAHexColor_Throws(string text)
    {
        // Act
        Action parse = () => AppIconSettings.ParseColor(text, "BackgroundColor");

        // Assert
        parse.ShouldThrow<AppIconException>().Message.ShouldContain("BackgroundColor");
    }

    [Fact]
    public void DrawWatchOS_IsAnOpaque1024Icon_WithThePictureAtItsSize()
    {
        // Arrange
        var settings = new AppIconSettings(Ring(), 0.65f, null, Navy, null);

        // Act
        using var icon = AppIconRenderer.DrawWatchOS(settings);
        using var pixels = SKBitmap.FromImage(icon);

        // Assert: the ring's 760 wide picture takes 65% of 1024, so its outer edge is 333 from the middle.
        icon.Width.ShouldBe(1024);
        icon.Height.ShouldBe(1024);
        icon.AlphaType.ShouldBe(SKAlphaType.Opaque);
        pixels.GetPixel(4, 4).ShouldBe(Navy);
        pixels.GetPixel(512, 512).ShouldBe(Lavender);
        pixels.GetPixel(512 - 325, 512).ShouldBe(Lavender);
        pixels.GetPixel(512 - 345, 512).ShouldBe(Navy);
    }

    [Fact]
    public void DrawWatchOS_TintColor_PaintsThePictureInIt()
    {
        // Arrange
        var settings = new AppIconSettings(Ring(), 0.65f, SKColors.White, Navy, null);

        // Act
        using var icon = AppIconRenderer.DrawWatchOS(settings);
        using var pixels = SKBitmap.FromImage(icon);

        // Assert
        pixels.GetPixel(512, 512).ShouldBe(SKColors.White);
        pixels.GetPixel(4, 4).ShouldBe(Navy);
    }

    [Fact]
    public void DrawWatchOS_PngPictureAndBackgroundFile_AreDrawn()
    {
        // Arrange
        var picture = Png("red.png", 100, 100, SKColors.Red);
        var background = Png("green.png", 10, 20, SKColors.Lime);
        var settings = new AppIconSettings(picture, 0.5f, null, Navy, background);

        // Act
        using var icon = AppIconRenderer.DrawWatchOS(settings);
        using var pixels = SKBitmap.FromImage(icon);

        // Assert: the background covers the whole icon; the picture takes the middle half.
        pixels.GetPixel(4, 4).ShouldBe(SKColors.Lime);
        pixels.GetPixel(512, 512).ShouldBe(SKColors.Red);
        pixels.GetPixel(240, 512).ShouldBe(SKColors.Lime);
    }

    [Fact]
    public void DrawWearOS_LayersCoverTheShownMiddle()
    {
        // Arrange
        var settings = new AppIconSettings(Ring(), 0.65f, null, Navy, null);

        // Act
        using var foreground = AppIconRenderer.DrawWearOSForeground(settings);
        using var background = AppIconRenderer.DrawWearOSBackground(settings);
        using var foregroundPixels = SKBitmap.FromImage(foreground);
        using var backgroundPixels = SKBitmap.FromImage(background);

        // Assert: 65% of the 288 pixels the launcher shows; around the picture the foreground is transparent.
        foreground.Width.ShouldBe(432);
        foregroundPixels.GetPixel(4, 4).Alpha.ShouldBe((byte)0);
        foregroundPixels.GetPixel(216, 216).ShouldBe(Lavender);
        foregroundPixels.GetPixel(216 - 90, 216).ShouldBe(Lavender);
        foregroundPixels.GetPixel(216 - 100, 216).Alpha.ShouldBe((byte)0);
        backgroundPixels.GetPixel(4, 4).ShouldBe(Navy);
        backgroundPixels.GetPixel(216, 216).ShouldBe(Navy);
    }

    [Fact]
    public void WriteWatchOS_WritesTheIconSet_AndKeepsTheCatalogsOwnContents()
    {
        // Arrange
        var catalog = Path.Combine(_folder, "Assets.xcassets");
        Directory.CreateDirectory(catalog);
        File.WriteAllText(Path.Combine(catalog, "Contents.json"), "the app's");

        // Act
        AppIconRenderer.WriteWatchOS(new AppIconSettings(Ring(), 0.65f, null, Navy, null), catalog);

        // Assert
        File.Exists(Path.Combine(catalog, "AppIcon.appiconset", "AppIcon.png")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(catalog, "AppIcon.appiconset", "Contents.json")).ShouldContain("\"platform\" : \"watchos\"");
        File.ReadAllText(Path.Combine(catalog, "Contents.json")).ShouldBe("the app's");
    }

    [Fact]
    public void WriteWearOS_WritesAnAdaptiveIcon_ThatNamesItsLayers()
    {
        // Arrange
        var resources = Path.Combine(_folder, "res");

        // Act
        AppIconRenderer.WriteWearOS(new AppIconSettings(Ring(), 0.65f, null, Navy, null), resources);

        // Assert
        var icon = File.ReadAllText(Path.Combine(resources, "mipmap-anydpi-v26", "appicon.xml"));
        icon.ShouldContain("<adaptive-icon");
        icon.ShouldContain("@mipmap/appicon_background");
        icon.ShouldContain("@mipmap/appicon_foreground");
        File.Exists(Path.Combine(resources, "mipmap-xxxhdpi", "appicon_background.png")).ShouldBeTrue();
        File.Exists(Path.Combine(resources, "mipmap-xxxhdpi", "appicon_foreground.png")).ShouldBeTrue();
    }

    [Fact]
    public void Draw_MissingOrUnsupportedPicture_Throws()
    {
        // Arrange
        var missing = new AppIconSettings(Path.Combine(_folder, "nope.svg"), 0.65f, null, Navy, null);
        var gif = Path.Combine(_folder, "logo.gif");
        File.WriteAllText(gif, "GIF89a");
        var unsupported = new AppIconSettings(gif, 0.65f, null, Navy, null);

        // Act
        Action drawMissing = () => AppIconRenderer.DrawWatchOS(missing);
        Action drawUnsupported = () => AppIconRenderer.DrawWatchOS(unsupported);

        // Assert
        drawMissing.ShouldThrow<AppIconException>().Message.ShouldContain("doesn't exist");
        drawUnsupported.ShouldThrow<AppIconException>().Message.ShouldContain("SVG, PNG or JPEG");
    }

    // The template's icon: a ring and a dot in a 760 wide square.
    private string Ring()
    {
        var path = Path.Combine(_folder, "ring.svg");
        File.WriteAllText(path, """
            <svg xmlns="http://www.w3.org/2000/svg" width="760" height="760" viewBox="-380 -380 760 760">
              <circle r="340" fill="none" stroke="#B9A6FF" stroke-width="80"/>
              <circle r="90" fill="#B9A6FF"/>
            </svg>
            """);
        return path;
    }

    private string Png(string name, int width, int height, SKColor color)
    {
        var path = Path.Combine(_folder, name);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
