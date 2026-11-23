using System.Text;
using System.Text.Json;
using Shouldly;
using Watchious.Messaging;
using Xunit;

namespace Watchious.Core.Tests;

// The Wear OS Data Layer format of Plugin.Maui.WearableMessaging, the phone side Watchious talks to: its
// WearableMessagingImplementation (Android) serializes Dictionary<string, string> to JSON on "/message", correlates
// answers with "_requestId"/"_replyToId", and keeps the application context as JSON in "/application_context".
public class MessagingTests
{
    [Fact]
    public async Task SendMessage_SendsTheJsonThePluginReads()
    {
        // Arrange
        var phone = new FakePhone();
        var messaging = new DataLayerPhoneMessaging(phone);

        // Act
        await messaging.SendMessageAsync(new Dictionary<string, string> { ["action"] = "start", ["reps"] = "10" });

        // Assert
        var (path, data) = phone.SentMessages.ShouldHaveSingleItem();
        path.ShouldBe("/message");
        JsonSerializer.Deserialize<Dictionary<string, string>>(data).ShouldBe(
            new Dictionary<string, string> { ["action"] = "start", ["reps"] = "10" },
            ignoreOrder: true);
    }

    [Fact]
    public async Task SendMessageWithReply_ReturnsThePhonesAnswer()
    {
        // Arrange
        var phone = new FakePhone();
        var messaging = new DataLayerPhoneMessaging(phone);
        phone.AnswerRequests(messaging, request => new Dictionary<string, string> { ["status"] = "ok", ["echo"] = request["action"] });

        // Act
        var answer = await messaging.SendMessageWithReplyAsync(new Dictionary<string, string> { ["action"] = "sync" });

        // Assert
        answer["status"].ShouldBe("ok");
        answer["echo"].ShouldBe("sync");
        var request = JsonSerializer.Deserialize<Dictionary<string, string>>(phone.SentMessages.Single().Data)!;
        request.ShouldContainKey("_requestId");
    }

    [Fact]
    public async Task SendMessageWithReply_NoAnswer_TimesOut()
    {
        // Arrange
        var messaging = new DataLayerPhoneMessaging(new FakePhone());

        // Act
        var send = () => messaging.SendMessageWithReplyAsync(new Dictionary<string, string> { ["action"] = "sync" }, TimeSpan.FromMilliseconds(50));

        // Assert
        await send.ShouldThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task MessageFromThePhoneThatWaits_IsAnsweredWithItsRequestId()
    {
        // Arrange
        var phone = new FakePhone();
        var messaging = new DataLayerPhoneMessaging(phone);
        PhoneMessageReceivedEventArgs? received = null;
        void OnMessageReceived(object? sender, PhoneMessageReceivedEventArgs args) => received = args;
        messaging.MessageReceived += OnMessageReceived;

        // Act
        messaging.OnMessage("/message", Encoding.UTF8.GetBytes("""{"action":"ping","_requestId":"abc"}"""));
        await received!.ReplyAsync(new Dictionary<string, string> { ["action"] = "pong" });
        messaging.MessageReceived -= OnMessageReceived;

        // Assert
        received.ExpectsReply.ShouldBeTrue();
        var answer = JsonSerializer.Deserialize<Dictionary<string, string>>(phone.SentMessages.Single().Data)!;
        answer["_replyToId"].ShouldBe("abc");
        answer["action"].ShouldBe("pong");
    }

    [Fact]
    public void MessageFromThePhone_WithoutRequestId_ExpectsNoReply()
    {
        // Arrange
        var messaging = new DataLayerPhoneMessaging(new FakePhone());
        PhoneMessageReceivedEventArgs? received = null;
        void OnMessageReceived(object? sender, PhoneMessageReceivedEventArgs args) => received = args;
        messaging.MessageReceived += OnMessageReceived;

        // Act
        messaging.OnMessage("/message", Encoding.UTF8.GetBytes("""{"action":"ping"}"""));
        messaging.MessageReceived -= OnMessageReceived;

        // Assert
        received!.Message["action"].ShouldBe("ping");
        received.ExpectsReply.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateApplicationContext_PutsTheDataItemThePluginReads()
    {
        // Arrange
        var phone = new FakePhone();
        var messaging = new DataLayerPhoneMessaging(phone);

        // Act
        await messaging.UpdateApplicationContextAsync(new Dictionary<string, string> { ["goal"] = "5000" });

        // Assert
        var (path, values) = phone.DataItems.ShouldHaveSingleItem();
        path.ShouldBe("/application_context");
        JsonSerializer.Deserialize<Dictionary<string, string>>(values["context"])!["goal"].ShouldBe("5000");
        long.Parse(values["timestamp"]).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ApplicationContextFromThePhone_ReadsItsValuesAsText()
    {
        // Arrange
        var messaging = new DataLayerPhoneMessaging(new FakePhone());
        var changes = 0;
        void OnChanged(object? sender, EventArgs args) => changes++;
        messaging.ApplicationContextChanged += OnChanged;

        // Act: the plugin serializes Dictionary<string, object>, so values may be numbers or booleans.
        messaging.OnDataItem("/application_context", "context", """{"name":"Leg day","sets":4,"done":false}""");
        messaging.OnDataItem("/application_context", "timestamp", "1763700000000");
        messaging.ApplicationContextChanged -= OnChanged;

        // Assert
        changes.ShouldBe(1);
        messaging.ReceivedApplicationContext.ShouldBe(
            new Dictionary<string, string> { ["name"] = "Leg day", ["sets"] = "4", ["done"] = "false" },
            ignoreOrder: true);
    }

    [Fact]
    public async Task Dispose_DisposesTheChannelOwner()
    {
        // Arrange
        var owner = new DisposableOwner();
        var messaging = new DataLayerPhoneMessaging(new FakePhone(), owner);

        // Act
        messaging.Dispose();

        // Assert
        owner.IsDisposed.ShouldBeTrue();
        (await messaging.IsPhoneReachableAsync()).ShouldBeTrue();
    }

    private sealed class FakePhone : IDataLayerChannel
    {
        private Func<Dictionary<string, string>, Dictionary<string, string>>? _answer;
        private DataLayerPhoneMessaging? _watch;

        public List<(string Path, byte[] Data)> SentMessages { get; } = [];

        public List<(string Path, IReadOnlyDictionary<string, string> Values)> DataItems { get; } = [];

        public void AnswerRequests(DataLayerPhoneMessaging watch, Func<Dictionary<string, string>, Dictionary<string, string>> answer)
        {
            _watch = watch;
            _answer = answer;
        }

        public Task<bool> IsPhoneConnectedAsync()
        {
            return Task.FromResult(true);
        }

        // Answers like the plugin's MessageReceived handler: the answer carries _replyToId.
        public Task SendMessageAsync(string path, byte[] data)
        {
            SentMessages.Add((path, data));
            var request = JsonSerializer.Deserialize<Dictionary<string, string>>(data)!;
            if (_watch is not null && _answer is not null && request.TryGetValue("_requestId", out var requestId))
            {
                var answer = _answer(request);
                answer["_replyToId"] = requestId;
                _ = Task.Run(() => _watch.OnMessage("/message", JsonSerializer.SerializeToUtf8Bytes(answer)));
            }

            return Task.CompletedTask;
        }

        public Task PutDataItemAsync(string path, IReadOnlyDictionary<string, string> values)
        {
            DataItems.Add((path, values));
            return Task.CompletedTask;
        }
    }

    private sealed class DisposableOwner : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
