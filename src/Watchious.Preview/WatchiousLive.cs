using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Watchious.Hosting;

namespace Watchious.Preview;

public sealed class WatchiousLiveOptions
{
    // ⚠️ http://0.0.0.0:5293, which a live host on another computer needs, lets every device on the network start the
    // app and tap in it: use it on a network you trust only.
    public string Url { get; set; } = "http://localhost:5293";
}

// Live mode: the app's C# runs here, under the debugger, and a live host (a watch host app without .NET, in the watchOS
// simulator or the Wear OS emulator) draws it with the real SwiftUI or Compose controls. The live host talks plain
// HTTP, like the inspector's app side, as that is what every watch allows:
//   POST /live/start?platform=wearos|watchos   starts a new instance of the app; the response is its session id
//   GET  /live/messages?session=...&ack=N     waits up to 20 s for what the app sends. Each message is a kind byte (1 a
//                                             patch batch, 2 JSON such as {"error": ...}), its sequence number and its
//                                             length (little-endian int64 and int32) and its bytes. N is the last
//                                             sequence number the live host got: the messages after it are sent again,
//                                             as an answer can be lost on the way, and the live host skips any it has.
//   POST /live/events?session=...             one message of the host: an event or {"applied": true}
//   POST /live/stop?session=...               ends the session
// A session whose live host asks for nothing for a minute ends too: the live host is gone.
public static class WatchiousLive
{
    public static async Task RunAsync(Func<WatchiousApp> createApp, WatchiousLiveOptions options, CancellationToken cancellationToken = default)
    {
        await using var server = Start(createApp, options);
        await server.StartAsync(cancellationToken);
        var hostUrl = options.Url.Replace("0.0.0.0", "<this computer>", StringComparison.Ordinal);
        Console.WriteLine($"Watchious live: {options.Url}");
        if (hostUrl != options.Url)
        {
            Console.WriteLine("⚠️ Every device on this network can start the app and tap in it.");
        }

        // The live hosts ship with Kebechet.Watchious.Sdk, whose WatchiousLive target starts them (Watchious.Sdk.Live.targets).
        Console.WriteLine("Start the live host in the app's folder: dotnet build -t:WatchiousLive -f net10.0-android (Wear OS) or -f net10.0-watchos (watchOS).");
        if (hostUrl != options.Url)
        {
            Console.WriteLine($"An Apple Watch reaches this app over the network: -f net10.0-watchos -r ios-arm64 -p:WatchiousDevice=<watch> -p:WatchiousLiveUrl={hostUrl}");
        }
        await server.WaitForShutdownAsync(cancellationToken);
    }

    internal static WebApplication Start(Func<WatchiousApp> createApp, WatchiousLiveOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(options.Url);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        // Created by the container, so that it disposes it, and with it every running app, when the server stops.
        builder.Services.AddSingleton(_ => new LiveSessions(createApp));
        var web = builder.Build();
        web.UseLocalOnlyRequests();

        web.MapPost("/live/start", (HttpContext context, LiveSessions sessions) =>
        {
            var platform = context.Request.Query["platform"] == "watchos" ? PreviewPlatform.watchOS : PreviewPlatform.WearOS;
            return Results.Text(sessions.Start(platform));
        });
        web.MapGet("/live/messages", async (HttpContext context, LiveSessions sessions, IHostApplicationLifetime lifetime) =>
        {
            var session = sessions.Find(context.Request.Query["session"]);
            if (session is null)
            {
                return Results.NotFound();
            }

            var ackText = context.Request.Query["ack"].ToString();
            var received = long.TryParse(ackText, NumberStyles.None, CultureInfo.InvariantCulture, out var ack) ? ack : 0;
            // Stopping the server ends the long requests; on its own it would wait for each of them.
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
            var messages = await session.TakeMessagesAsync(received, TimeSpan.FromSeconds(20), stopping.Token);
            return Results.Bytes(messages, "application/octet-stream");
        });
        web.MapPost("/live/events", async (HttpContext context, LiveSessions sessions) =>
        {
            var session = sessions.Find(context.Request.Query["session"]);
            if (session is null)
            {
                return Results.NotFound();
            }

            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body, context.RequestAborted);
            session.Receive(body.GetBuffer().AsSpan(0, (int)body.Length));
            return Results.NoContent();
        });
        web.MapPost("/live/stop", async (HttpContext context, LiveSessions sessions) =>
        {
            await sessions.StopAsync(context.Request.Query["session"]);
            return Results.NoContent();
        });
        return web;
    }
}

internal sealed class LiveSessions : IAsyncDisposable
{
    private readonly Func<WatchiousApp> _createApp;
    private readonly ConcurrentDictionary<string, LiveSession> _sessions = new();
    private readonly CancellationTokenSource _disposed = new();
    private readonly Task _sweeping;

    public LiveSessions(Func<WatchiousApp> createApp)
    {
        _createApp = createApp;
        _sweeping = SweepAsync(_disposed.Token);
    }

    public string Start(PreviewPlatform platform)
    {
        var id = Guid.NewGuid().ToString("N");
        _sessions[id] = new LiveSession(AppRun.Start(_createApp, platform));
        return id;
    }

    public LiveSession? Find(string? id)
    {
        return id is not null && _sessions.TryGetValue(id, out var session) ? session : null;
    }

    public async Task StopAsync(string? id)
    {
        if (id is not null && _sessions.TryRemove(id, out var session))
        {
            await session.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _disposed.CancelAsync();
        await _sweeping;
        foreach (var id in _sessions.Keys)
        {
            await StopAsync(id);
        }

        _disposed.Dispose();
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(LiveSession.SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                foreach (var (id, session) in _sessions)
                {
                    if (session.IsGoneAfterSweep())
                    {
                        await StopAsync(id);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

internal sealed class LiveSession(AppRun run) : IAsyncDisposable
{
    public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    private const byte PatchBatch = 1;
    private const byte Json = 2;
    private const int HeaderBytes = 1 + sizeof(long) + sizeof(int);
    // A long answer would keep the live host from showing the first batches; the rest come with the next request.
    private const int MaxAnswerBytes = 1024 * 1024;
    // A minute of sweeps. Counted in sweeps rather than read from the clock, so that a server paused at a breakpoint,
    // or on a computer that slept, keeps the session of the live host that waited for it.
    private const int MaxSilentSweeps = 6;

    private readonly Lock _lock = new();
    // What the live host hasn't confirmed it got, oldest first, numbered from 1.
    private readonly Queue<(long Sequence, SessionMessage Message)> _unconfirmed = new();
    // One request at a time reads the app's messages. Not disposed: a request may still release it, and without a
    // wait handle it holds nothing to free.
    private readonly SemaphoreSlim _reading = new(1, 1);
    private long _lastSequence;
    private CancellationTokenSource? _waiting;
    private int _silentSweeps;

    public bool IsGoneAfterSweep()
    {
        return Interlocked.Increment(ref _silentSweeps) > MaxSilentSweeps;
    }

    public void Receive(ReadOnlySpan<byte> json)
    {
        Touch();
        run.Receive(json);
    }

    // The messages after the received sequence number: first those the live host may have lost, then what the app sent
    // since, or nothing when it sends nothing within the wait. A newer request ends an older one that still waits, as
    // the live host gave up on that one.
    public async Task<byte[]> TakeMessagesAsync(long received, TimeSpan wait, CancellationToken cancellationToken)
    {
        Touch();
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waiting.CancelAfter(wait);
        lock (_lock)
        {
            _waiting?.Cancel();
            _waiting = waiting;
            while (_unconfirmed.TryPeek(out var oldest) && oldest.Sequence <= received)
            {
                _unconfirmed.Dequeue();
            }
        }

        try
        {
            await _reading.WaitAsync(waiting.Token);
            try
            {
                if (!HasUnconfirmed())
                {
                    var hasMessages = await run.Outgoing.WaitToReadAsync(waiting.Token);
                    if (!hasMessages)
                    {
                        return [];
                    }
                }

                return Answer();
            }
            finally
            {
                _reading.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // The wait is over, a newer request took over, or the server stops; what the app sent waits for the next
            // request.
            return [];
        }
        finally
        {
            lock (_lock)
            {
                if (_waiting == waiting)
                {
                    _waiting = null;
                }
            }

            Touch();
        }
    }

    public ValueTask DisposeAsync()
    {
        return run.DisposeAsync();
    }

    private bool HasUnconfirmed()
    {
        lock (_lock)
        {
            return _unconfirmed.Count > 0;
        }
    }

    // The unconfirmed messages again, then the new ones, numbered on.
    private byte[] Answer()
    {
        var answer = new ArrayBufferWriter<byte>();
        lock (_lock)
        {
            foreach (var (sequence, message) in _unconfirmed)
            {
                if (answer.WrittenCount >= MaxAnswerBytes)
                {
                    break;
                }

                Write(answer, sequence, message);
            }

            while (answer.WrittenCount < MaxAnswerBytes && run.Outgoing.TryRead(out var message))
            {
                var sequence = ++_lastSequence;
                _unconfirmed.Enqueue((sequence, message));
                Write(answer, sequence, message);
            }
        }

        return answer.WrittenSpan.ToArray();
    }

    private static void Write(ArrayBufferWriter<byte> answer, long sequence, SessionMessage message)
    {
        var header = answer.GetSpan(HeaderBytes);
        header[0] = message.IsText ? Json : PatchBatch;
        BinaryPrimitives.WriteInt64LittleEndian(header[1..], sequence);
        BinaryPrimitives.WriteInt32LittleEndian(header[(1 + sizeof(long))..], message.Bytes.Length);
        answer.Advance(HeaderBytes);
        answer.Write(message.Bytes);
    }

    private void Touch()
    {
        Interlocked.Exchange(ref _silentSweeps, 0);
    }
}
