using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace Watchious.Messaging;

// WatchConnectivity through the SwiftUI host (watchOS/WatchConnectivity.swift, which Watchious.Sdk compiles into the
// host). WCSession's property-list dictionaries cross the C boundary as JSON objects of strings; answers to a
// message use WatchConnectivity's own reply handler, as the plugin does on the iPhone.
internal sealed partial class WatchConnectivityPhoneMessaging : IPhoneMessaging, IDisposable
{
    private static readonly TimeSpan DefaultReplyTimeout = TimeSpan.FromSeconds(5);

    // The registered instance, which the native callbacks (static by nature) reach. The service is a singleton.
    private static WatchConnectivityPhoneMessaging? _current;

    private readonly ConcurrentDictionary<long, TaskCompletionSource<IReadOnlyDictionary<string, string>>> _pendingReplies = new();
    private long _lastRequestId;
    private IReadOnlyDictionary<string, string> _receivedApplicationContext = new Dictionary<string, string>();

    public unsafe WatchConnectivityPhoneMessaging()
    {
        _current = this;
        Start(&OnMessage, &OnReply, &OnApplicationContext);
    }

    public event EventHandler<PhoneMessageReceivedEventArgs>? MessageReceived;

    public event EventHandler? ApplicationContextChanged;

    public IReadOnlyDictionary<string, string> ReceivedApplicationContext => _receivedApplicationContext;

    public Task<bool> IsPhoneReachableAsync()
    {
        return Task.FromResult(IsReachable() != 0);
    }

    public Task SendMessageAsync(IReadOnlyDictionary<string, string> message)
    {
        if (Send(Encoding.UTF8.GetString(MessageJson.Encode(message)), requestId: 0) != 0)
        {
            return Task.FromException(new InvalidOperationException("The phone isn't reachable: WatchConnectivity sends messages only while the phone app can receive them."));
        }

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<string, string>> SendMessageWithReplyAsync(
        IReadOnlyDictionary<string, string> message,
        TimeSpan? timeout = null)
    {
        var requestId = Interlocked.Increment(ref _lastRequestId);
        var reply = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReplies[requestId] = reply;
        try
        {
            if (Send(Encoding.UTF8.GetString(MessageJson.Encode(message)), requestId) != 0)
            {
                throw new InvalidOperationException("The phone isn't reachable: WatchConnectivity sends messages only while the phone app can receive them.");
            }

            return await reply.Task.WaitAsync(timeout ?? DefaultReplyTimeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"The phone didn't answer within {(timeout ?? DefaultReplyTimeout).TotalSeconds} seconds.");
        }
        finally
        {
            _pendingReplies.TryRemove(requestId, out _);
        }
    }

    public Task UpdateApplicationContextAsync(IReadOnlyDictionary<string, string> context)
    {
        if (UpdateApplicationContext(Encoding.UTF8.GetString(MessageJson.Encode(context))) != 0)
        {
            throw new InvalidOperationException("WatchConnectivity didn't take the application context; its session may not be active yet.");
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Interlocked.CompareExchange(ref _current, null, this);
    }

    // The native callbacks must not let exceptions escape into Swift.
    [UnmanagedCallersOnly]
    private static unsafe void OnMessage(long replyToken, byte* json)
    {
        try
        {
            var current = _current;
            var message = MessageJson.Decode(new ReadOnlySpan<byte>(json, NativeLength(json)));
            if (current is null || message is null)
            {
                return;
            }

            Func<IReadOnlyDictionary<string, string>, Task>? reply = null;
            if (replyToken != 0)
            {
                reply = answer =>
                {
                    Reply(replyToken, Encoding.UTF8.GetString(MessageJson.Encode(answer)));
                    return Task.CompletedTask;
                };
            }

            current.MessageReceived?.Invoke(current, new PhoneMessageReceivedEventArgs(message, reply));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious.Messaging: {exception}");
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void OnReply(long requestId, byte* json, byte* error)
    {
        try
        {
            var current = _current;
            if (current is null || !current._pendingReplies.TryRemove(requestId, out var reply))
            {
                return;
            }

            if (error is not null)
            {
                reply.TrySetException(new InvalidOperationException(Marshal.PtrToStringUTF8((nint)error)));
                return;
            }

            var message = MessageJson.Decode(new ReadOnlySpan<byte>(json, NativeLength(json)));
            reply.TrySetResult(message ?? new Dictionary<string, string>());
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious.Messaging: {exception}");
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void OnApplicationContext(byte* json)
    {
        try
        {
            var current = _current;
            var context = MessageJson.Decode(new ReadOnlySpan<byte>(json, NativeLength(json)));
            if (current is null || context is null)
            {
                return;
            }

            current._receivedApplicationContext = context;
            current.ApplicationContextChanged?.Invoke(current, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious.Messaging: {exception}");
        }
    }

    private static unsafe int NativeLength(byte* text)
    {
        return text is null ? 0 : MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text).Length;
    }

    [LibraryImport("__Internal", EntryPoint = "watchious_messaging_start")]
    private static unsafe partial void Start(
        delegate* unmanaged<long, byte*, void> onMessage,
        delegate* unmanaged<long, byte*, byte*, void> onReply,
        delegate* unmanaged<byte*, void> onApplicationContext);

    [LibraryImport("__Internal", EntryPoint = "watchious_messaging_is_reachable")]
    private static partial int IsReachable();

    // requestId 0 sends without waiting for an answer. Returns 0 when WatchConnectivity took the message.
    [LibraryImport("__Internal", EntryPoint = "watchious_messaging_send", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Send(string json, long requestId);

    [LibraryImport("__Internal", EntryPoint = "watchious_messaging_reply", StringMarshalling = StringMarshalling.Utf8)]
    private static partial void Reply(long replyToken, string json);

    [LibraryImport("__Internal", EntryPoint = "watchious_messaging_update_context", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int UpdateApplicationContext(string json);
}
