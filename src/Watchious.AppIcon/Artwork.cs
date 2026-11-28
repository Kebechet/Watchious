using SkiaSharp;
using Svg.Skia;

namespace Watchious.AppIcon;

// A picture or background from a file: an SVG, drawn as vectors at whatever size it is drawn, or a PNG or JPEG.
internal sealed class Artwork : IDisposable
{
    private readonly SKSvg? _svg;
    private readonly SKPicture? _picture;
    private readonly SKImage? _image;

    private Artwork(SKSvg svg, SKPicture picture)
    {
        _svg = svg;
        _picture = picture;
        Bounds = picture.CullRect;
    }

    private Artwork(SKImage image)
    {
        _image = image;
        Bounds = SKRect.Create(image.Width, image.Height);
    }

    public SKRect Bounds { get; }

    public static Artwork Load(string path, string setting)
    {
        if (!File.Exists(path))
        {
            throw new AppIconException($"{setting} '{path}' doesn't exist.");
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var artwork = extension switch
        {
            ".svg" => LoadSvg(path, setting),
            ".png" or ".jpg" or ".jpeg" => LoadImage(path, setting),
            _ => throw new AppIconException($"{setting} '{path}' is a {extension} file: use an SVG, PNG or JPEG."),
        };
        if (artwork.Bounds.Width <= 0 || artwork.Bounds.Height <= 0)
        {
            artwork.Dispose();
            throw new AppIconException($"{setting} '{path}' has no size: give the SVG a width and height, or a viewBox.");
        }

        return artwork;
    }

    // As large as fits into the box, centered in it.
    public void DrawInside(SKCanvas canvas, SKRect box, SKPaint? paint = null)
    {
        var scale = Math.Min(box.Width / Bounds.Width, box.Height / Bounds.Height);
        Draw(canvas, Centered(box, scale), paint);
    }

    // Filling the box, centered, and cut where it is longer than the box.
    public void DrawCovering(SKCanvas canvas, SKRect box)
    {
        var scale = Math.Max(box.Width / Bounds.Width, box.Height / Bounds.Height);
        canvas.Save();
        canvas.ClipRect(box);
        Draw(canvas, Centered(box, scale), null);
        canvas.Restore();
    }

    public void Dispose()
    {
        _svg?.Dispose();
        _image?.Dispose();
    }

    private static Artwork LoadSvg(string path, string setting)
    {
        var svg = new SKSvg();
        var picture = svg.Load(path);
        if (picture is null)
        {
            svg.Dispose();
            throw new AppIconException($"{setting} '{path}' isn't an SVG that can be drawn.");
        }

        return new Artwork(svg, picture);
    }

    private static Artwork LoadImage(string path, string setting)
    {
        var image = SKImage.FromEncodedData(path);
        if (image is null)
        {
            throw new AppIconException($"{setting} '{path}' isn't a PNG or JPEG that can be read.");
        }

        return new Artwork(image);
    }

    private SKRect Centered(SKRect box, float scale)
    {
        var width = Bounds.Width * scale;
        var height = Bounds.Height * scale;
        return SKRect.Create(box.MidX - width / 2, box.MidY - height / 2, width, height);
    }

    private void Draw(SKCanvas canvas, SKRect destination, SKPaint? paint)
    {
        if (_picture is not null)
        {
            canvas.Save();
            canvas.Translate(destination.Left, destination.Top);
            canvas.Scale(destination.Width / Bounds.Width, destination.Height / Bounds.Height);
            canvas.Translate(-Bounds.Left, -Bounds.Top);
            canvas.DrawPicture(_picture, paint);
            canvas.Restore();
        }
        else if (_image is not null)
        {
            canvas.DrawImage(_image, destination, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
        }
    }
}
