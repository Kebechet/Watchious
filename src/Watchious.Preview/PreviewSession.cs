using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Watchious.Hosting;

namespace Watchious.Preview;

// The platform look the page shows. The names keep their brands' casing, like WatchPlatform.
internal enum PreviewPlatform
{
    WearOS,
    watchOS,
}

// One page load: a new instance of the app, its patches to the page as binary messages, exactly as the watch
// hosts get them, and the page's events back as small JSON messages {"id": 1, "name": "click", "value": null}. The page
// confirms each batch it has applied with {"applied": true}.
internal sealed class PreviewSession(WebSocket socket, Func<WatchiousApp> createApp, PreviewPlatform platform)
{
    private readonly Channel<OutgoingMessage> _outgoing = Channel.CreateUnbounded<OutgoingMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        WatchiousApp app;
        using (WatchiousPlatform.ConfigureServices(services => PreviewEssentials.AddTo(services, platform)))
        {
            app = createApp();
        }

        await using (app)
        {
            var host = new SessionHost(_outgoing.Writer);
            var sending = SendAsync(cancellationToken);
            try
            {
                // Neither the start nor a handler is awaited, as on the watch hosts: the page's messages are read on,
                // so a page whose OnInitializedAsync keeps running, or a handler that waits for a later click, can't
                // hold up the clicks or the closing of the page, which disposes the app.
                _ = ObserveAsync(() => app.StartAsync(host), host);
                await ReceiveAsync(app, host, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                host.ReportError(exception);
            }
            finally
            {
                host.ReleasePendingBatches();
                _outgoing.Writer.TryComplete();
                await sending;
            }
        }
    }

    private async Task ReceiveAsync(WatchiousApp app, SessionHost host, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var pageMessage = ParseMessage(message.ToArray());
            message.SetLength(0);
            if (pageMessage is null)
            {
                continue;
            }

            if (pageMessage.Value.IsBatchApplied)
            {
                host.BatchApplied();
                continue;
            }

            var pageEvent = pageMessage.Value;
            _ = ObserveAsync(() => app.DispatchEventAsync(pageEvent.EventHandlerId, pageEvent.Name, pageEvent.Value), host);
        }
    }

    private static async Task ObserveAsync(Func<Task> action, SessionHost host)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            host.ReportError(exception);
        }
    }

    // A malformed message is skipped: one bad message must not end the session.
    private static PageMessage? ParseMessage(byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("applied", out _))
            {
                return new PageMessage(IsBatchApplied: true, 0, "", null);
            }

            if (!root.TryGetProperty("id", out var id) || !root.TryGetProperty("name", out var name))
            {
                return null;
            }

            var value = root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString()
                : null;
            return new PageMessage(IsBatchApplied: false, id.GetUInt64(), name.GetString() ?? "", value);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _outgoing.Reader.ReadAllAsync(cancellationToken))
            {
                await socket.SendAsync(message.Bytes, message.Type, endOfMessage: true, cancellationToken);
            }

            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            // The page went away; there is no one left to tell.
        }
    }

    private readonly record struct PageMessage(bool IsBatchApplied, ulong EventHandlerId, string Name, string? Value);

    private readonly record struct OutgoingMessage(byte[] Bytes, WebSocketMessageType Type);

    // Renders run on the renderer's dispatcher while the socket sends on its own; the channel keeps the batches in
    // their order.
    private sealed class SessionHost(ChannelWriter<OutgoingMessage> writer) : IWatchiousHost
    {
        // The batches the page hasn't confirmed yet, oldest first. Blazor keeps the event handlers a batch replaces until
        // its task completes, so a click the page sent before it applied the batch still finds its handler.
        private readonly ConcurrentQueue<TaskCompletionSource> _pendingBatches = new();

        public Task ApplyPatches(ReadOnlySpan<byte> patches)
        {
            var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingBatches.Enqueue(applied);
            if (!writer.TryWrite(new OutgoingMessage(patches.ToArray(), WebSocketMessageType.Binary)))
            {
                ReleasePendingBatches();
            }

            return applied.Task;
        }

        public void BatchApplied()
        {
            if (_pendingBatches.TryDequeue(out var applied))
            {
                applied.TrySetResult();
            }
        }

        // The page is gone, so nothing will confirm these.
        public void ReleasePendingBatches()
        {
            while (_pendingBatches.TryDequeue(out var applied))
            {
                applied.TrySetResult();
            }
        }

        public void ReportError(Exception exception)
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = exception.ToString() });
            writer.TryWrite(new OutgoingMessage(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text));
        }
    }
}
