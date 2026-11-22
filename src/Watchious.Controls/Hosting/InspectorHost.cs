using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Watchious.Hosting;

// Mirrors a running app to the Watchious inspector (dotnet run -f net10.0 -- --inspector on the developer's
// machine): every patch batch the native host gets, the events the watch dispatches and the errors, and it
// dispatches the events the inspector page sends. The native host keeps working if the inspector is unreachable.
internal sealed class InspectorHost : IWatchiousHost, IAsyncDisposable
{
    // Batches wait here until the connection is up; past this many unsent messages the inspector is given up, since
    // a mirror with a batch missing would show a wrong tree.
    private const int MaxPendingMessages = 10_000;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly IWatchiousHost _host;
    private readonly Func<ulong, string, string?, Task> _dispatchEventAsync;
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Channel<OutgoingMessage> _outgoing = Channel.CreateBounded<OutgoingMessage>(
        new BoundedChannelOptions(MaxPendingMessages) { SingleReader = true });

    private readonly Task _connection;
    private volatile bool _isStopped;

    public InspectorHost(IWatchiousHost host, Uri inspectorUrl, Func<ulong, string, string?, Task> dispatchEventAsync)
    {
        _host = host;
        _dispatchEventAsync = dispatchEventAsync;
        _connection = RunAsync(inspectorUrl, _stopping.Token);
    }

    public Task ApplyPatches(ReadOnlySpan<byte> patches)
    {
        var applied = _host.ApplyPatches(patches);
        if (!_isStopped)
        {
            Enqueue(new OutgoingMessage(patches.ToArray(), WebSocketMessageType.Binary));
        }

        return applied;
    }

    public void ReportError(Exception exception)
    {
        _host.ReportError(exception);
        WriteJson(new Dictionary<string, string> { ["error"] = exception.ToString() });
    }

    public void ReportEvent(ulong eventHandlerId, string eventName, string? value)
    {
        WriteJson(new InspectorEvent(eventHandlerId, eventName, value));
    }

    public async ValueTask DisposeAsync()
    {
        StopMirroring();
        await _stopping.CancelAsync();
        try
        {
            await _connection;
        }
        catch (OperationCanceledException)
        {
        }

        _socket.Dispose();
        _stopping.Dispose();
    }

    private void WriteJson<T>(T message)
    {
        if (_isStopped)
        {
            return;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(message, InspectorJsonContext.Default.GetTypeInfo(typeof(T))!);
        Enqueue(new OutgoingMessage(json, WebSocketMessageType.Text));
    }

    private void Enqueue(OutgoingMessage message)
    {
        if (!_outgoing.Writer.TryWrite(message))
        {
            StopMirroring();
        }
    }

    // Nothing more is queued: the inspector is unreachable, went away or fell too far behind.
    private void StopMirroring()
    {
        _isStopped = true;
        _outgoing.Writer.TryComplete();
    }

    private async Task RunAsync(Uri inspectorUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(ConnectTimeout);
            await _socket.ConnectAsync(inspectorUrl, connectTimeout.Token);
        }
        catch (Exception)
        {
            // No inspector listening, or an address that isn't ws:// or wss://: the app runs on without it.
            StopMirroring();
            return;
        }

        var receiving = ReceiveEventsAsync(cancellationToken);
        try
        {
            await foreach (var message in _outgoing.Reader.ReadAllAsync(cancellationToken))
            {
                await _socket.SendAsync(message.Bytes, message.Type, endOfMessage: true, cancellationToken);
            }

            // Given up: the page must not go on clicking in an app it no longer mirrors.
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
        }
        finally
        {
            StopMirroring();
        }

        await receiving;
    }

    private async Task ReceiveEventsAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var pageEvent = ReadEvent(message.ToArray());
                message.SetLength(0);
                if (pageEvent is not null)
                {
                    await DispatchFromPageAsync(pageEvent);
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
        }
        finally
        {
            StopMirroring();
        }
    }

    // A malformed message is skipped: one bad message must not end the inspector's clicks.
    private static InspectorEvent? ReadEvent(byte[] json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, InspectorJsonContext.Default.InspectorEvent);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task DispatchFromPageAsync(InspectorEvent pageEvent)
    {
        try
        {
            await _dispatchEventAsync(pageEvent.Id, pageEvent.Name, pageEvent.Value);
        }
        catch (Exception exception)
        {
            ReportError(exception);
        }
    }

    private readonly record struct OutgoingMessage(byte[] Bytes, WebSocketMessageType Type);
}

// The inspector's event message, in both directions: {"id": 1, "name": "click", "value": null}.
internal sealed record InspectorEvent(ulong Id, string Name, string? Value);
