using System.Collections.Concurrent;
using System.Text;

namespace Watchious.Messaging;

// What the Wear OS Data Layer offers, which DataLayerPhoneMessaging builds on: messages to the connected phone,
// and data items that reach it even when it isn't running.
internal interface IDataLayerChannel
{
    Task<bool> IsPhoneConnectedAsync();

    Task SendMessageAsync(string path, byte[] data);

    Task PutDataItemAsync(string path, IReadOnlyDictionary<string, string> values);
}

// Plugin.Maui.WearableMessaging's Data Layer format: messages on "/message" as a UTF-8 JSON object of strings; a
// message that waits for an answer carries "_requestId", and the answer is another "/message" carrying
// "_replyToId" with the same value; the application context is the data item "/application_context" with the JSON
// in "context" and the time in "timestamp".
// owner, if any, is disposed with it: on Wear OS the channel, whose listeners must be removed.
internal sealed class DataLayerPhoneMessaging(IDataLayerChannel channel, IDisposable? owner = null) : IPhoneMessaging, IDisposable
{
    public const string MessagePath = "/message";
    public const string ApplicationContextPath = "/application_context";
    public const string RequestIdKey = "_requestId";
    public const string ReplyToIdKey = "_replyToId";

    private static readonly TimeSpan DefaultReplyTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<IReadOnlyDictionary<string, string>>> _pendingReplies = new();
    private IReadOnlyDictionary<string, string> _receivedApplicationContext = new Dictionary<string, string>();

    public event EventHandler<PhoneMessageReceivedEventArgs>? MessageReceived;

    public event EventHandler? ApplicationContextChanged;

    public IReadOnlyDictionary<string, string> ReceivedApplicationContext => _receivedApplicationContext;

    public Task<bool> IsPhoneReachableAsync()
    {
        return channel.IsPhoneConnectedAsync();
    }

    public void Dispose()
    {
        owner?.Dispose();
    }

    public Task SendMessageAsync(IReadOnlyDictionary<string, string> message)
    {
        return channel.SendMessageAsync(MessagePath, MessageJson.Encode(message));
    }

    public async Task<IReadOnlyDictionary<string, string>> SendMessageWithReplyAsync(
        IReadOnlyDictionary<string, string> message,
        TimeSpan? timeout = null)
    {
        var requestId = Guid.NewGuid().ToString();
        var reply = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReplies[requestId] = reply;
        try
        {
            var request = new Dictionary<string, string>(message) { [RequestIdKey] = requestId };
            await channel.SendMessageAsync(MessagePath, MessageJson.Encode(request));
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
        var values = new Dictionary<string, string>
        {
            ["context"] = Encoding.UTF8.GetString(MessageJson.Encode(context)),
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return channel.PutDataItemAsync(ApplicationContextPath, values);
    }

    // From the channel: a message the phone sent.
    public void OnMessage(string path, byte[] data)
    {
        if (path != MessagePath || data.Length == 0)
        {
            return;
        }

        var message = MessageJson.Decode(data);
        if (message is null)
        {
            return;
        }

        if (message.TryGetValue(ReplyToIdKey, out var replyToId))
        {
            if (_pendingReplies.TryGetValue(replyToId, out var pending))
            {
                pending.TrySetResult(message);
            }

            return;
        }

        Func<IReadOnlyDictionary<string, string>, Task>? reply = null;
        if (message.TryGetValue(RequestIdKey, out var requestId))
        {
            reply = answer => SendMessageAsync(new Dictionary<string, string>(answer) { [ReplyToIdKey] = requestId });
        }

        MessageReceived?.Invoke(this, new PhoneMessageReceivedEventArgs(message, reply));
    }

    // From the channel: a data item the phone put. The plugin keeps the application context as JSON in "context".
    public void OnDataItem(string path, string key, string? value)
    {
        if (path != ApplicationContextPath || key != "context" || value is null)
        {
            return;
        }

        var context = MessageJson.Decode(Encoding.UTF8.GetBytes(value));
        if (context is null)
        {
            return;
        }

        _receivedApplicationContext = context;
        ApplicationContextChanged?.Invoke(this, EventArgs.Empty);
    }
}
