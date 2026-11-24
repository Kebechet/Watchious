using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Watchious.Preview;

public sealed class WatchiousInspectorOptions
{
    // http://0.0.0.0:5292 lets watches reach it over the network, and with it anyone on that network, so only on a
    // trusted one; with adb reverse tcp:5292 tcp:5292, Wear OS emulators and USB devices reach localhost.
    public string Url { get; set; } = "http://localhost:5292";

    public bool OpenBrowser { get; set; } = true;

    public string ImagesDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Resources", "Images");
}

// The live inspector: a running watch app (started with --watchious-inspector=http://<this>/inspector/app) mirrors
// its patches, events and errors here, and the page shows them next to the screen they build. Clicks on that
// screen are dispatched in the app on the watch. The app talks plain HTTP (see InspectorHost in Watchious.Controls),
// because that is all a real Apple Watch allows; the pages, on this computer, use a WebSocket.
public static class WatchiousInspector
{
    public static async Task RunAsync(WatchiousInspectorOptions options, CancellationToken cancellationToken = default)
    {
        await using var server = Start(options);
        await server.StartAsync(cancellationToken);
        var appUrl = options.Url.Replace("0.0.0.0", "<this computer>", StringComparison.Ordinal);
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
        web.UseLocalOnlyRequests();
        web.UseWebSockets();

        var relay = new InspectorRelay();
        web.MapGet("/", () => Results.Content(PageResources.Read("inspector.html"), "text/html"));
        web.MapGet("/preview.js", () => Results.Content(PageResources.Read("preview.js"), "text/javascript"));
        web.MapGet("/inspector.js", () => Results.Content(PageResources.Read("inspector.js"), "text/javascript"));
        web.MapGet("/preview.css", () => Results.Content(PageResources.Read("preview.css"), "text/css"));
        web.MapGet("/images/{name}", (string name) => PreviewImages.Find(options.ImagesDirectory, name));
        web.MapPost("/inspector/app/start", () => Results.Text(relay.StartApp()));
        web.MapPost("/inspector/app/messages", async (HttpContext context) =>
        {
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body, context.RequestAborted);
            var isCurrent = relay.ReceiveFromApp(Session(context), body.ToArray());
            return isCurrent ? Results.Ok() : Results.StatusCode(StatusCodes.Status410Gone);
        });
        web.MapGet("/inspector/app/events", async (HttpContext context) =>
        {
            using var stopping = Stopping(context);
            var events = await relay.TakeEventsAsync(Session(context), stopping.Token);
            return events is null ? Results.StatusCode(StatusCodes.Status410Gone) : Results.Bytes(events, "application/json");
        });
        web.MapPost("/inspector/app/stop", (HttpContext context) =>
        {
            relay.StopApp(Session(context));
            return Results.Ok();
        });
        web.Map("/inspector/page", AcceptPageAsync);
        return web;

        async Task AcceptPageAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var stopping = Stopping(context);
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await relay.RunPageAsync(socket, stopping.Token);
        }
    }

    private static string Session(HttpContext context)
    {
        return context.Request.Query["session"].ToString();
    }

    // Stopping the server ends the long requests; on its own it would wait for each of them.
    private static CancellationTokenSource Stopping(HttpContext context)
    {
        var lifetime = context.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        return CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
    }
}

// Keeps every batch of the current app, so a page opened later replays them and builds the same tree. Batches,
// events and errors reach each page in the order the app sent them: every page has its own queue, filled under one
// lock.
internal sealed class InspectorRelay
{
    private const byte BatchFrame = 1;

    // How long a request for the page's events stays open when there are none, and how long an app may go without a
    // request before it counts as gone; it asks again as soon as a request returns.
    private static readonly TimeSpan EventsPollTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan AppTimeout = TimeSpan.FromSeconds(30);

    private static readonly byte[] ResetMessage = Encoding.UTF8.GetBytes("""{"status":"reset"}""");
    private static readonly byte[] ConnectedMessage = Encoding.UTF8.GetBytes("""{"status":"connected"}""");
    private static readonly byte[] WaitingMessage = Encoding.UTF8.GetBytes("""{"status":"waiting"}""");

    private readonly Lock _lock = new();
    private readonly List<Page> _pages = [];
    private readonly List<byte[]> _batches = [];
    private AppSession? _app;

    public string StartApp()
    {
        var app = new AppSession(Guid.NewGuid().ToString("N"), OnAppTimedOut);
        lock (_lock)
        {
            _app?.End();
            _app = app;
            _batches.Clear();
            foreach (var page in _pages)
            {
                page.Enqueue(ResetMessage, WebSocketMessageType.Text);
            }
        }

        app.Touch();
        return app.Id;
    }

    // False when another app started since: this one's node ids would mix with the new app's tree.
    public bool ReceiveFromApp(string session, byte[] frames)
    {
        lock (_lock)
        {
            var app = CurrentApp(session);
            if (app is null)
            {
                return false;
            }

            app.Touch();
            foreach (var (kind, payload) in ReadFrames(frames))
            {
                var type = kind == BatchFrame ? WebSocketMessageType.Binary : WebSocketMessageType.Text;
                if (type == WebSocketMessageType.Binary)
                {
                    _batches.Add(payload);
                }

                foreach (var page in _pages)
                {
                    page.Enqueue(payload, type);
                }
            }

            return true;
        }
    }

    // The page's events as a JSON array, empty after a while without any; null when the app is no longer the current
    // one.
    public async Task<byte[]?> TakeEventsAsync(string session, CancellationToken cancellationToken)
    {
        AppSession? app;
        lock (_lock)
        {
            app = CurrentApp(session);
        }

        if (app is null)
        {
            return null;
        }

        app.Touch();
        return await app.TakeEventsAsync(EventsPollTimeout, cancellationToken);
    }

    public void StopApp(string session)
    {
        lock (_lock)
        {
            if (CurrentApp(session) is not null)
            {
                EndApp();
            }
        }
    }

    public async Task RunPageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var page = new Page(socket);
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
            await foreach (var (bytes, type) in Page.ReceiveAsync(socket, cancellationToken))
            {
                if (type != WebSocketMessageType.Text)
                {
                    continue;
                }

                lock (_lock)
                {
                    _app?.Enqueue(bytes);
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

    private AppSession? CurrentApp(string session)
    {
        return _app is not null && _app.Id == session ? _app : null;
    }

    private void OnAppTimedOut(AppSession app)
    {
        lock (_lock)
        {
            if (_app == app)
            {
                EndApp();
            }
        }
    }

    // Under the lock.
    private void EndApp()
    {
        _app?.End();
        _app = null;
        foreach (var page in _pages)
        {
            page.Enqueue(WaitingMessage, WebSocketMessageType.Text);
        }
    }

    // InspectorHost's frames: a kind byte, a little-endian length and the payload. A truncated frame ends the list.
    private static List<(byte Kind, byte[] Payload)> ReadFrames(byte[] frames)
    {
        var result = new List<(byte Kind, byte[] Payload)>();
        var position = 0;
        while (frames.Length - position >= 5)
        {
            var kind = frames[position];
            var length = BinaryPrimitives.ReadInt32LittleEndian(frames.AsSpan(position + 1, 4));
            if (length < 0 || length > frames.Length - position - 5)
            {
                break;
            }

            result.Add((kind, frames.AsSpan(position + 5, length).ToArray()));
            position += 5 + length;
        }

        return result;
    }

    private sealed class AppSession
    {
        private static readonly byte[] NoEvents = "[]"u8.ToArray();

        private readonly Channel<byte[]> _events = Channel.CreateUnbounded<byte[]>();
        private readonly CancellationTokenSource _ended = new();
        private readonly Timer _timeout;

        public AppSession(string id, Action<AppSession> timedOut)
        {
            Id = id;
            _timeout = new Timer(_ => timedOut(this));
        }

        public string Id { get; }

        public void Touch()
        {
            _timeout.Change(AppTimeout, Timeout.InfiniteTimeSpan);
        }

        public void Enqueue(byte[] pageEvent)
        {
            _events.Writer.TryWrite(pageEvent);
        }

        public void End()
        {
            _events.Writer.TryComplete();
            _ended.Cancel();
            _timeout.Dispose();
        }

        public async Task<byte[]?> TakeEventsAsync(TimeSpan wait, CancellationToken cancellationToken)
        {
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _ended.Token);
            waiting.CancelAfter(wait);
            try
            {
                var hasEvents = await _events.Reader.WaitToReadAsync(waiting.Token);
                if (!hasEvents)
                {
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                return _ended.IsCancellationRequested ? null : NoEvents;
            }

            var events = new List<byte[]>();
            while (_events.Reader.TryRead(out var pageEvent))
            {
                events.Add(pageEvent);
            }

            using var json = new MemoryStream();
            json.WriteByte((byte)'[');
            for (var index = 0; index < events.Count; index++)
            {
                if (index > 0)
                {
                    json.WriteByte((byte)',');
                }

                json.Write(events[index]);
            }

            json.WriteByte((byte)']');
            return json.ToArray();
        }
    }

    private sealed class Page(WebSocket socket)
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
                // The page went away.
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
