using System.Buffers;
using System.Buffers.Binary;
using System.Text.Json;
using System.Threading.Channels;

namespace Watchious.Hosting;

// Mirrors a running app to the Watchious inspector (dotnet run -f net10.0 -- --inspector on the developer's
// machine): every patch batch the native host gets, the events the watch dispatches and the errors, and it
// dispatches the events the inspector page sends. The native host keeps working if the inspector is unreachable.
//
// Plain HTTP requests, because a real Apple Watch gives apps no sockets, and WebSockets only while they stream audio
// (https://developer.apple.com/documentation/technotes/tn3135-low-level-networking-on-watchos); URLSession's HTTP is what
// it allows. POST <url>/start opens a session, POST <url>/messages?session=... sends what is queued, as frames of a
// kind byte (1 a patch batch, 2 JSON), a little-endian length and the payload, GET <url>/events?session=... waits for
// the page's clicks, and POST <url>/stop?session=... ends the session.
internal sealed class InspectorHost : IWatchiousHost, IAsyncDisposable
{
    public const byte BatchFrame = 1;
    public const byte JsonFrame = 2;

    // Batches wait here until the connection is up; past this many unsent messages the inspector is given up, since
    // a mirror with a batch missing would show a wrong tree.
    private const int MaxPendingMessages = 10_000;

    private const int MaxRequestBytes = 256 * 1024;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    // Longer than the inspector holds a GET for the page's events open (20 s).
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(40);

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly IWatchiousHost _host;
    private readonly Func<ulong, string, string?, Task> _dispatchEventAsync;
    private readonly HttpClient _http = CreateHttpClient();
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
            Enqueue(new OutgoingMessage(BatchFrame, patches.ToArray()));
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

        _http.Dispose();
        _stopping.Dispose();
    }

    // Android's own HTTP stack refuses plain http:// (cleartext traffic) unless the app allows it, so there .NET's
    // sockets handler talks to the inspector, which adb reverse makes localhost. Elsewhere the platform's handler: on
    // watchOS URLSession, the only way out of a real watch.
    private static HttpClient CreateHttpClient()
    {
        HttpMessageHandler handler = OperatingSystem.IsAndroid() ? new SocketsHttpHandler() : new HttpClientHandler();
        return new HttpClient(handler) { Timeout = RequestTimeout };
    }

    private void WriteJson<T>(T message)
    {
        if (_isStopped)
        {
            return;
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(message, InspectorJsonContext.Default.GetTypeInfo(typeof(T))!);
        Enqueue(new OutgoingMessage(JsonFrame, json));
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
        string session;
        try
        {
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(ConnectTimeout);
            using var response = await _http.PostAsync(Endpoint(inspectorUrl, "start"), content: null, connectTimeout.Token);
            response.EnsureSuccessStatusCode();
            session = await response.Content.ReadAsStringAsync(connectTimeout.Token);
        }
        catch (Exception exception)
        {
            // No inspector listening, or an address that isn't http:// or https://: the app runs on without it.
            Console.Error.WriteLine($"Watchious: the inspector at {inspectorUrl} can't be reached ({exception.Message}); the app runs without it.");
            StopMirroring();
            return;
        }

        var query = "?session=" + Uri.EscapeDataString(session);
        var receiving = ReceiveEventsAsync(Endpoint(inspectorUrl, "events" + query), cancellationToken);
        try
        {
            await SendAsync(Endpoint(inspectorUrl, "messages" + query), cancellationToken);
        }
        finally
        {
            StopMirroring();
            // Given up or disposed: the page must not go on clicking in an app it no longer mirrors.
            await TellStoppedAsync(Endpoint(inspectorUrl, "stop" + query));
        }

        await receiving;
    }

    private async Task SendAsync(Uri messagesUrl, CancellationToken cancellationToken)
    {
        var request = new ArrayBufferWriter<byte>();
        try
        {
            while (await _outgoing.Reader.WaitToReadAsync(cancellationToken))
            {
                request.ResetWrittenCount();
                while (request.WrittenCount < MaxRequestBytes && _outgoing.Reader.TryRead(out var message))
                {
                    WriteFrame(request, message);
                }

                using var content = new ByteArrayContent(request.WrittenSpan.ToArray());
                using var response = await _http.PostAsync(messagesUrl, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    // Another app started since, or the inspector forgot this one.
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
        }
    }

    private static void WriteFrame(ArrayBufferWriter<byte> request, OutgoingMessage message)
    {
        var header = request.GetSpan(5);
        header[0] = message.Kind;
        BinaryPrimitives.WriteInt32LittleEndian(header[1..], message.Payload.Length);
        request.Advance(5);
        request.Write(message.Payload);
    }

    private async Task ReceiveEventsAsync(Uri eventsUrl, CancellationToken cancellationToken)
    {
        try
        {
            while (!_isStopped)
            {
                using var response = await _http.GetAsync(eventsUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                var json = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                foreach (var pageEvent in ReadEvents(json))
                {
                    // Not awaited, as on the watch hosts: a handler that waits for a later click must not hold that click
                    // up.
                    _ = DispatchFromPageAsync(pageEvent);
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
        }
        finally
        {
            StopMirroring();
        }
    }

    private async Task TellStoppedAsync(Uri stopUrl)
    {
        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            using var response = await _http.PostAsync(stopUrl, content: null, timeout.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // The inspector notices on its own when the app stops asking for events.
        }
    }

    // The inspector sends only JSON objects; a batch that still can't be read is skipped, so that it doesn't end the
    // inspector's clicks.
    private static InspectorEvent[] ReadEvents(byte[] json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, InspectorJsonContext.Default.InspectorEventArray) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // An exception in the app's handler reaches the watch through the renderer. What fails here is the dispatch
    // itself, such as a click on a handler that a newer batch replaced before the page showed it: that is the
    // inspector's problem, so only the inspector hears of it and the watch keeps its screen.
    private async Task DispatchFromPageAsync(InspectorEvent pageEvent)
    {
        try
        {
            await _dispatchEventAsync(pageEvent.Id, pageEvent.Name, pageEvent.Value);
        }
        catch (Exception exception)
        {
            WriteJson(new Dictionary<string, string> { ["error"] = exception.ToString() });
        }
    }

    private static Uri Endpoint(Uri inspectorUrl, string relative)
    {
        return new Uri(inspectorUrl.AbsoluteUri.TrimEnd('/') + "/" + relative);
    }

    private readonly record struct OutgoingMessage(byte Kind, byte[] Payload);
}

// The inspector's event message, in both directions: {"id": 1, "name": "click", "value": null}.
internal sealed record InspectorEvent(ulong Id, string Name, string? Value);
