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
// hosts get them, and the page's events back as small JSON messages {"id": 1, "name": "click", "value": null}.
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
                await app.StartAsync(host);
                await ReceiveEventsAsync(app, host, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                host.ReportError(exception);
            }
            finally
            {
                _outgoing.Writer.TryComplete();
                await sending;
            }
        }
    }

    private async Task ReceiveEventsAsync(WatchiousApp app, SessionHost host, CancellationToken cancellationToken)
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

            var nativeEvent = ParseEvent(message.ToArray());
            message.SetLength(0);
            if (nativeEvent is null)
            {
                continue;
            }

            try
            {
                await app.DispatchEventAsync(nativeEvent.Value.EventHandlerId, nativeEvent.Value.Name, nativeEvent.Value.Value);
            }
            catch (Exception exception)
            {
                host.ReportError(exception);
            }
        }
    }

    private static (ulong EventHandlerId, string Name, string? Value)? ParseEvent(byte[] json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var id) || !root.TryGetProperty("name", out var name))
        {
            return null;
        }

        var value = root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.String
            ? valueElement.GetString()
            : null;
        return (id.GetUInt64(), name.GetString() ?? "", value);
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

    private readonly record struct OutgoingMessage(byte[] Bytes, WebSocketMessageType Type);

    // Renders run on the renderer's dispatcher while the socket sends on its own; the channel keeps the batches in
    // their order.
    private sealed class SessionHost(ChannelWriter<OutgoingMessage> writer) : IWatchiousHost
    {
        // The page doesn't confirm the batches it applied.
        public Task ApplyPatches(ReadOnlySpan<byte> patches)
        {
            writer.TryWrite(new OutgoingMessage(patches.ToArray(), WebSocketMessageType.Binary));
            return Task.CompletedTask;
        }

        public void ReportError(Exception exception)
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = exception.ToString() });
            writer.TryWrite(new OutgoingMessage(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text));
        }
    }
}
