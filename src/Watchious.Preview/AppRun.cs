using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Watchious.Hosting;

namespace Watchious.Preview;

// What a run of the app sends its client: a batch of patches (protocol v2, exactly as the watch hosts get them), or JSON
// text such as {"error": "..."}.
internal readonly record struct SessionMessage(byte[] Bytes, bool IsText);

// What a client sends: an event, {"id": 1, "name": "click", "value": null}, or {"applied": true} once it shows a batch.
internal readonly record struct ClientMessage(bool IsBatchApplied, ulong EventHandlerId, string Name, string? Value)
{
    // A malformed message is skipped: one bad message must not end the session.
    public static ClientMessage? Parse(ReadOnlySpan<byte> json)
    {
        try
        {
            var reader = new Utf8JsonReader(json);
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.TryGetProperty("applied", out _))
            {
                return new ClientMessage(IsBatchApplied: true, 0, "", null);
            }

            if (!root.TryGetProperty("id", out var id) || !root.TryGetProperty("name", out var name))
            {
                return null;
            }

            var value = root.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString()
                : null;
            return new ClientMessage(IsBatchApplied: false, id.GetUInt64(), name.GetString() ?? "", value);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}

// One run of the app for one client (a preview page or a live host): a new instance of the app with the preview's
// stand-ins for the watch APIs, its messages queued in order for the client, and the client's events dispatched.
internal sealed class AppRun : IAsyncDisposable
{
    private readonly Channel<SessionMessage> _outgoing = Channel.CreateUnbounded<SessionMessage>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly WatchiousApp _app;
    private readonly SessionHost _host;

    private AppRun(Func<WatchiousApp> createApp, PreviewPlatform platform)
    {
        using (WatchiousPlatform.ConfigureServices(services => PreviewEssentials.AddTo(services, platform)))
        {
            _app = createApp();
        }

        _host = new SessionHost(_outgoing.Writer);
    }

    public ChannelReader<SessionMessage> Outgoing => _outgoing.Reader;

    // Neither the start nor a handler is awaited, as on the watch hosts: the client's messages are read on, so a page
    // whose OnInitializedAsync keeps running, or a handler that waits for a later click, can't hold up the clicks or
    // the end of the run, which disposes the app. The start runs on the thread pool, as the first render runs the
    // pages' synchronous OnInitialized, which mustn't hold up the request that started the run.
    public static AppRun Start(Func<WatchiousApp> createApp, PreviewPlatform platform)
    {
        var run = new AppRun(createApp, platform);
        _ = run.ObserveAsync(() => Task.Run(() => run._app.StartAsync(run._host)));
        return run;
    }

    public void Receive(ReadOnlySpan<byte> json)
    {
        var message = ClientMessage.Parse(json);
        if (message is null)
        {
            return;
        }

        if (message.Value.IsBatchApplied)
        {
            _host.BatchApplied();
            return;
        }

        var clientEvent = message.Value;
        _ = ObserveAsync(() => _app.DispatchEventAsync(clientEvent.EventHandlerId, clientEvent.Name, clientEvent.Value));
    }

    public void ReportError(Exception exception)
    {
        _host.ReportError(exception);
    }

    // No more messages: the client is gone, so nothing will confirm the batches it hasn't.
    public void Complete()
    {
        _host.ReleasePendingBatches();
        _outgoing.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        Complete();
        await _app.DisposeAsync();
    }

    private async Task ObserveAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _host.ReportError(exception);
        }
    }

    // Renders run on the renderer's dispatcher while the client is sent its messages on its own; the channel keeps the
    // batches in their order.
    private sealed class SessionHost(ChannelWriter<SessionMessage> writer) : IWatchiousHost
    {
        // The batches the client hasn't confirmed yet, oldest first. Blazor keeps the event handlers a batch replaces
        // until its task completes, so a click the client sent before it applied the batch still finds its handler.
        private readonly ConcurrentQueue<TaskCompletionSource> _pendingBatches = new();

        public Task ApplyPatches(ReadOnlySpan<byte> patches)
        {
            var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingBatches.Enqueue(applied);
            if (!writer.TryWrite(new SessionMessage(patches.ToArray(), IsText: false)))
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
            writer.TryWrite(new SessionMessage(Encoding.UTF8.GetBytes(json), IsText: true));
        }
    }
}
