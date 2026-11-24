using System.Net.WebSockets;
using Watchious.Hosting;

namespace Watchious.Preview;

// The platform look the page shows. The names keep their brands' casing, like WatchPlatform.
internal enum PreviewPlatform
{
    WearOS,
    watchOS,
}

// One page load: an AppRun whose patches go to the page as binary WebSocket messages and its errors as text,
// with the page's events and confirmations ({"applied": true}) coming back as small JSON messages.
internal sealed class PreviewSession(WebSocket socket, Func<WatchiousApp> createApp, PreviewPlatform platform)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var session = AppRun.Start(createApp, platform);
        var sending = SendAsync(session, cancellationToken);
        try
        {
            await ReceiveAsync(session, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            session.ReportError(exception);
        }
        finally
        {
            session.Complete();
            await sending;
        }
    }

    private async Task ReceiveAsync(AppRun session, CancellationToken cancellationToken)
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

            session.Receive(message.GetBuffer().AsSpan(0, (int)message.Length));
            message.SetLength(0);
        }
    }

    private async Task SendAsync(AppRun session, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in session.Outgoing.ReadAllAsync(cancellationToken))
            {
                var type = message.IsText ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
                await socket.SendAsync(message.Bytes, type, endOfMessage: true, cancellationToken);
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
}
