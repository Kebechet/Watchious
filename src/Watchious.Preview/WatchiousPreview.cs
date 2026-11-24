using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Watchious.Hosting;

namespace Watchious.Preview;

public sealed class WatchiousPreviewOptions
{
    public string Url { get; set; } = "http://localhost:5290";

    public bool OpenBrowser { get; set; } = true;

    // The app's images (Resources/Images); Watchious.Sdk copies them next to the preview.
    public string ImagesDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Resources", "Images");
}

// Runs a Watchious app on the desktop: a local page renders it in a watch frame from the same patches the watch hosts
// get, and sends taps back as events. Each page load starts a new instance of the app. The app runs in this
// process, so a .NET debugger stops in its pages and services.
public static class WatchiousPreview
{
    // The generated Main of a Watchious app's preview target: dotnet run -f net10.0 [-- --urls=... --no-browser],
    // -- --inspector for the live inspector of the app running on a watch, or -- --live for live mode.
    public static async Task RunAsync(Func<WatchiousApp> createApp, string[] args, CancellationToken cancellationToken = default)
    {
        var urlArgument = args.FirstOrDefault(argument => argument.StartsWith("--urls=", StringComparison.Ordinal));
        var url = urlArgument?["--urls=".Length..];
        var openBrowser = !args.Contains("--no-browser");
        if (args.Contains("--live"))
        {
            var liveOptions = new WatchiousLiveOptions();
            liveOptions.Url = url ?? liveOptions.Url;
            await WatchiousLive.RunAsync(createApp, liveOptions, cancellationToken);
            return;
        }

        if (args.Contains("--inspector"))
        {
            var inspectorOptions = new WatchiousInspectorOptions { OpenBrowser = openBrowser };
            inspectorOptions.Url = url ?? inspectorOptions.Url;
            await WatchiousInspector.RunAsync(inspectorOptions, cancellationToken);
            return;
        }

        var options = new WatchiousPreviewOptions { OpenBrowser = openBrowser };
        options.Url = url ?? options.Url;
        await RunAsync(createApp, options, cancellationToken);
    }

    public static async Task RunAsync(Func<WatchiousApp> createApp, WatchiousPreviewOptions options, CancellationToken cancellationToken = default)
    {
        await using var server = Start(createApp, options);
        await server.StartAsync(cancellationToken);
        Console.WriteLine($"Watchious preview: {options.Url}");
        if (options.OpenBrowser)
        {
            Process.Start(new ProcessStartInfo(options.Url) { UseShellExecute = true });
        }

        await server.WaitForShutdownAsync(cancellationToken);
    }

    internal static WebApplication Start(Func<WatchiousApp> createApp, WatchiousPreviewOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(options.Url);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var web = builder.Build();
        web.UseLocalOnlyRequests();
        web.UseWebSockets();

        web.MapGet("/", () => Results.Content(PageResources.Read("index.html"), "text/html"));
        web.MapGet("/preview.js", () => Results.Content(PageResources.Read("preview.js"), "text/javascript"));
        web.MapGet("/preview.css", () => Results.Content(PageResources.Read("preview.css"), "text/css"));
        web.MapGet("/images/{name}", (string name) => PreviewImages.Find(options.ImagesDirectory, name));
        web.Map("/session", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var platform = context.Request.Query["platform"] == "watchos" ? PreviewPlatform.watchOS : PreviewPlatform.WearOS;
            // Stopping the server ends the sessions; on its own it would wait for each page to close them.
            var lifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await new PreviewSession(socket, createApp, platform).RunAsync(stopping.Token);
        });
        return web;
    }
}

internal static class PageResources
{
    public static string Read(string name)
    {
        using var stream = typeof(PageResources).Assembly.GetManifestResourceStream("wwwroot/" + name)
            ?? throw new InvalidOperationException($"The preview page resource {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

internal static class PreviewImages
{
    // Image names are restricted to letters, digits and _ (Watchious.Sdk checks them), so a name can't leave the
    // folder.
    public static IResult Find(string directory, string name)
    {
        if (!name.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
        {
            return Results.NotFound();
        }

        foreach (var (extension, contentType) in new[] { (".png", "image/png"), (".jpg", "image/jpeg") })
        {
            var path = Path.Combine(directory, name + extension);
            if (File.Exists(path))
            {
                return Results.File(path, contentType);
            }
        }

        return Results.NotFound();
    }
}
