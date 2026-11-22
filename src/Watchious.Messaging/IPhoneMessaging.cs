namespace Watchious.Messaging;

// Messages between the watch app and its phone app. The phone side is Plugin.Maui.WearableMessaging
// (https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging), unchanged: Watchious speaks its
// formats, the Wearable Data Layer on Wear OS and WatchConnectivity on watchOS. The platforms require that on
// Wear OS both apps share the applicationId and signing key, and that on watchOS the watch app is the iPhone app's
// companion.
public interface IPhoneMessaging
{
    Task<bool> IsPhoneReachableAsync();

    Task SendMessageAsync(IReadOnlyDictionary<string, string> message);

    // Waits for the phone's answer (MessageReceivedEventArgs.Reply... on the phone), 5 seconds by default as in the
    // plugin.
    Task<IReadOnlyDictionary<string, string>> SendMessageWithReplyAsync(
        IReadOnlyDictionary<string, string> message,
        TimeSpan? timeout = null);

    // The latest state for the phone, which it gets even if it isn't running now.
    Task UpdateApplicationContextAsync(IReadOnlyDictionary<string, string> context);

    // The phone's latest application context.
    IReadOnlyDictionary<string, string> ReceivedApplicationContext { get; }

    // Raised on a background thread. Subscribers unsubscribe when they are disposed.
    event EventHandler<PhoneMessageReceivedEventArgs>? MessageReceived;

    event EventHandler? ApplicationContextChanged;
}

public sealed class PhoneMessageReceivedEventArgs(
    IReadOnlyDictionary<string, string> message,
    Func<IReadOnlyDictionary<string, string>, Task>? reply) : EventArgs
{
    public IReadOnlyDictionary<string, string> Message { get; } = message;

    // The phone waits for an answer (SendMessageWithReplyAsync on the phone).
    public bool ExpectsReply => reply is not null;

    public Task ReplyAsync(IReadOnlyDictionary<string, string> answer)
    {
        if (reply is null)
        {
            throw new InvalidOperationException("The phone doesn't wait for an answer to this message.");
        }

        return reply(answer);
    }
}
