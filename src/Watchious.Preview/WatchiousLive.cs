using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
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
    public string Url { get; set; } = "http://localhost:5293";
}

// Live mode: the app's C# runs here, under the debugger, and a live host (a watch host app without .NET, in the watchOS
// simulator or the Wear OS emulator) draws it with the real SwiftUI or Compose controls. The live host talks plain
// HTTP, like the inspector's app side, as that is what every watch allows:
//   POST /live/start?platform=wearos|watchos   starts a new instance of the app; the response is its session id
//   GET  /live/messages?session=...           waits up to 20 s for what the app sends, each as a kind byte (1 a patch
//                                             batch, 2 JSON such as {"error": ...}), a little-endian length and the bytes
//   POST /live/events?session=...             one message of the host: an event or {"applied": true}
//   POST /live/stop?session=...               ends the session
// A session nobody asks for messages for a minute ends too: its live host is gone.
public static class WatchiousLive
{
    public static async Task RunAsync(Func<WatchiousApp> createApp, WatchiousLiveOptions options, CancellationToken cancellationToken = default)
    {
        await using var server = Start(createApp, options);
        await server.StartAsync(cancellationToken);
        var hostUrl = options.Url.Replace("0.0.0.0", "<this computer>", StringComparison.Ordinal);
        Console.WriteLine($"Watchious live: {options.Url}");
        Console.WriteLine($"Start the live host with {hostUrl} (see the README's Live mode).");
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
        web.MapGet("/live/messages", async (HttpContext context, LiveSessions sessions) =>
        {
            var session = sessions.Find(context.Request.Query["session"]);
            if (session is null)
            {
                return Results.NotFound();
            }

            var messages = await session.TakeMessagesAsync(TimeSpan.FromSeconds(20), context.RequestAborted);
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
    private static readonly TimeSpan MaxSilence = TimeSpan.FromMinutes(1);

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
        _sessions[id] = new LiveSession(AppSession.Start(_createApp, platform));
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
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                foreach (var (id, session) in _sessions)
                {
                    if (session.IsSilentFor(MaxSilence))
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

internal sealed class LiveSession(AppSession session) : IAsyncDisposable
{
    private const byte PatchBatch = 1;
    private const byte Json = 2;
    // A long answer would keep the live host from showing the first batches; the rest come with the next request.
    private const int MaxAnswerBytes = 1024 * 1024;

    private long _lastContact = Environment.TickCount64;

    public bool IsSilentFor(TimeSpan silence)
    {
        return Environment.TickCount64 - Interlocked.Read(ref _lastContact) > silence.TotalMilliseconds;
    }

    public void Receive(ReadOnlySpan<byte> json)
    {
        Touch();
        session.Receive(json);
    }

    // What the app sent since the last request, or nothing when it sends nothing within the wait.
    public async Task<byte[]> TakeMessagesAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        Touch();
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waiting.CancelAfter(wait);
        try
        {
            if (!await session.Outgoing.WaitToReadAsync(waiting.Token))
            {
                return [];
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        finally
        {
            Touch();
        }

        var answer = new ArrayBufferWriter<byte>();
        while (answer.WrittenCount < MaxAnswerBytes && session.Outgoing.TryRead(out var message))
        {
            var header = answer.GetSpan(5);
            header[0] = message.IsText ? Json : PatchBatch;
            BinaryPrimitives.WriteInt32LittleEndian(header[1..], message.Bytes.Length);
            answer.Advance(5);
            answer.Write(message.Bytes);
        }

        return answer.WrittenSpan.ToArray();
    }

    public ValueTask DisposeAsync()
    {
        return session.DisposeAsync();
    }

    private void Touch()
    {
        Interlocked.Exchange(ref _lastContact, Environment.TickCount64);
    }
}
