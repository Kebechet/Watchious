using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Watchious.Preview;

public sealed class WatchiousInspectorOptions
{
    // http://0.0.0.0:5292 lets watches reach it over the network; with adb reverse tcp:5292 tcp:5292, Wear OS
    // emulators and USB devices reach localhost.
    public string Url { get; set; } = "http://localhost:5292";

    public bool OpenBrowser { get; set; } = true;

    public string ImagesDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Resources", "Images");
}

// The live inspector: a running watch app (started with --watchious-inspector=ws://<this>/inspector/app) mirrors
// its patches, events and errors here, and the page shows them next to the screen they build. Clicks on that
// screen are dispatched in the app on the watch.
public static class WatchiousInspector
{
    public static async Task RunAsync(WatchiousInspectorOptions options, CancellationToken cancellationToken = default)
    {
        await using var server = Start(options);
        await server.StartAsync(cancellationToken);
        var appUrl = options.Url.Replace("http://", "ws://", StringComparison.Ordinal).Replace("0.0.0.0", "<this computer>", StringComparison.Ordinal);
        Console.WriteLine($"Watchious inspector: {options.Url}");
        Console.WriteLine($"Start the watch app with --watchious-inspector={appUrl}/inspector/app");
        if (options.OpenBrowser)
        {
            Process.Start(new ProcessStartInfo(options.Url.Replace("0.0.0.0", "localhost", StringComparison.Ordinal)) { UseShellExecute = true });
        }

        await server.WaitForShutdownAsync(cancellationToken);
    }

    internal static WebApplication Start(WatchiousInspectorOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(options.Url);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var web = builder.Build();
        web.UseWebSockets();

        var relay = new InspectorRelay();
        web.MapGet("/", () => Results.Content(PageResources.Read("inspector.html"), "text/html"));
        web.MapGet("/preview.js", () => Results.Content(PageResources.Read("preview.js"), "text/javascript"));
        web.MapGet("/inspector.js", () => Results.Content(PageResources.Read("inspector.js"), "text/javascript"));
        web.MapGet("/preview.css", () => Results.Content(PageResources.Read("preview.css"), "text/css"));
        web.MapGet("/images/{name}", (string name) => PreviewImages.Find(options.ImagesDirectory, name));
        web.Map("/inspector/app", context => AcceptAsync(context, relay.RunAppAsync));
        web.Map("/inspector/page", context => AcceptAsync(context, relay.RunPageAsync));
        return web;
    }

    private static async Task AcceptAsync(HttpContext context, Func<WebSocket, CancellationToken, Task> run)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await run(socket, context.RequestAborted);
    }
}

// Keeps every batch of the connected app, so a page opened later replays them and builds the same tree. Batches,
// events and errors reach each page in the order the app sent them: every peer has its own queue, filled under one
// lock.
internal sealed class InspectorRelay
{
    private static readonly byte[] ResetMessage = Encoding.UTF8.GetBytes("""{"status":"reset"}""");
    private static readonly byte[] ConnectedMessage = Encoding.UTF8.GetBytes("""{"status":"connected"}""");
    private static readonly byte[] WaitingMessage = Encoding.UTF8.GetBytes("""{"status":"waiting"}""");

    private readonly Lock _lock = new();
    private readonly List<Peer> _pages = [];
    private readonly List<byte[]> _batches = [];
    private Peer? _app;

    public async Task RunAppAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var app = new Peer(socket);
        lock (_lock)
        {
            _app?.Complete();
            _app = app;
            _batches.Clear();
            foreach (var page in _pages)
            {
                page.Enqueue(ResetMessage, WebSocketMessageType.Text);
            }
        }

        var sending = app.SendQueuedAsync(cancellationToken);
        try
        {
            await foreach (var (bytes, type) in Peer.ReceiveAsync(socket, cancellationToken))
            {
                lock (_lock)
                {
                    if (type == WebSocketMessageType.Binary)
                    {
                        _batches.Add(bytes);
                    }

                    foreach (var page in _pages)
                    {
                        page.Enqueue(bytes, type);
                    }
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                if (_app == app)
                {
                    _app = null;
                    foreach (var page in _pages)
                    {
                        page.Enqueue(WaitingMessage, WebSocketMessageType.Text);
                    }
                }
            }

            app.Complete();
            await sending;
        }
    }

    public async Task RunPageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var page = new Peer(socket);
        lock (_lock)
        {
            page.Enqueue(_app is null ? WaitingMessage : ConnectedMessage, WebSocketMessageType.Text);
            foreach (var batch in _batches)
            {
                page.Enqueue(batch, WebSocketMessageType.Binary);
            }

            _pages.Add(page);
        }

        var sending = page.SendQueuedAsync(cancellationToken);
        try
        {
            await foreach (var (bytes, type) in Peer.ReceiveAsync(socket, cancellationToken))
            {
                if (type != WebSocketMessageType.Text)
                {
                    continue;
                }

                lock (_lock)
                {
                    _app?.Enqueue(bytes, WebSocketMessageType.Text);
                }
            }
        }
        finally
        {
            lock (_lock)
            {
                _pages.Remove(page);
            }

            page.Complete();
            await sending;
        }
    }

    private sealed class Peer(WebSocket socket)
    {
        private readonly Channel<(byte[] Bytes, WebSocketMessageType Type)> _queue =
            Channel.CreateUnbounded<(byte[] Bytes, WebSocketMessageType Type)>(new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(byte[] bytes, WebSocketMessageType type)
        {
            _queue.Writer.TryWrite((bytes, type));
        }

        public void Complete()
        {
            _queue.Writer.TryComplete();
        }

        public async Task SendQueuedAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var (bytes, type) in _queue.Reader.ReadAllAsync(cancellationToken))
                {
                    await socket.SendAsync(bytes, type, endOfMessage: true, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
            {
                // The other side went away.
            }
        }

        public static async IAsyncEnumerable<(byte[] Bytes, WebSocketMessageType Type)> ReceiveAsync(
            WebSocket socket,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            using var message = new MemoryStream();
            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                }
                catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
                {
                    yield break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    yield break;
                }

                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    yield return (message.ToArray(), result.MessageType);
                    message.SetLength(0);
                }
            }
        }
    }
}
