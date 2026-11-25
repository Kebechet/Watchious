using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;
using Watchious.Preview;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

// The live server as a live host uses it: patches in, events and confirmations out, over plain HTTP.
public class LiveTests
{
    [Fact]
    public async Task LiveHost_GetsTheAppsPatches_AndItsClicksReachTheApp()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await StartServerAsync(cancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };
        var start = await http.PostAsync("/live/start?platform=watchos", content: null, cancellationToken);
        var host = new FakeLiveHost(http, await start.Content.ReadAsStringAsync(cancellationToken));
        await host.ReceiveUntilAsync(() => host.Mirror.Root.Descendants().Any(IsPlusOne), cancellationToken);

        // Act
        var click = await host.ClickAsync(host.Mirror.Root.Descendants().Single(IsPlusOne), cancellationToken);
        await host.ReceiveUntilAsync(() => host.Mirror.Root.Descendants().Any(node => node.Content == "Count: 1"), cancellationToken);

        // Assert
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        click.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        host.Mirror.Root.Descendants().ShouldContain(node => node.Content == "Count: 1");
    }

    [Fact]
    public async Task Messages_TheLiveHostDidntConfirm_AreSentAgain()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await StartServerAsync(cancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };
        var start = await http.PostAsync("/live/start?platform=wearos", content: null, cancellationToken);
        var session = await start.Content.ReadAsStringAsync(cancellationToken);
        var lost = await http.GetByteArrayAsync($"/live/messages?session={session}&ack=0", cancellationToken);

        // Act
        var again = await http.GetByteArrayAsync($"/live/messages?session={session}&ack=0", cancellationToken);

        // Assert
        lost.ShouldNotBeEmpty();
        ReadMessages(lost)[0].Sequence.ShouldBe(1);
        again.Take(lost.Length).ShouldBe(lost);
    }

    [Fact]
    public async Task Messages_TheLiveHostConfirmed_AreNotSentAgain()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await StartServerAsync(cancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };
        var start = await http.PostAsync("/live/start?platform=wearos", content: null, cancellationToken);
        var session = await start.Content.ReadAsStringAsync(cancellationToken);
        var host = new FakeLiveHost(http, session);
        await host.ReceiveUntilAsync(() => host.Mirror.Root.Descendants().Any(IsPlusOne), cancellationToken);
        var received = host.LastSequence;
        await host.ClickAsync(host.Mirror.Root.Descendants().Single(IsPlusOne), cancellationToken);

        // Act
        var answer = await http.GetByteArrayAsync($"/live/messages?session={session}&ack={received}", cancellationToken);

        // Assert
        var messages = ReadMessages(answer);
        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(message => message.Sequence > received);
    }

    [Fact]
    public async Task Requests_OfAStoppedOrUnknownSession_AreNotFound()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = await StartServerAsync(cancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };
        var start = await http.PostAsync("/live/start?platform=wearos", content: null, cancellationToken);
        var session = await start.Content.ReadAsStringAsync(cancellationToken);

        // Act
        await http.PostAsync($"/live/stop?session={session}", content: null, cancellationToken);
        var stopped = await http.GetAsync($"/live/messages?session={session}", cancellationToken);
        var unknown = await http.GetAsync("/live/messages?session=unknown", cancellationToken);
        var unknownEvent = await http.PostAsJsonAsync("/live/events?session=unknown", new { applied = true }, cancellationToken);

        // Assert
        stopped.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownEvent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("http://localhost:5293", "dotnet build -t:WatchiousLive -tl:off")]
    [InlineData("http://localhost:5300", "dotnet build -t:WatchiousLive -tl:off -p:WatchiousLivePort=5300")]
    [InlineData("http://0.0.0.0:5300", "dotnet build -t:WatchiousLive -tl:off -p:WatchiousLivePort=5300")]
    public void StartLiveHostCommand_PassesAnotherPortThanTheDefault_ToTheTarget(string listeningUrl, string expected)
    {
        // Act
        var command = WatchiousLive.StartLiveHostCommand(listeningUrl);

        // Assert
        command.ShouldBe(expected);
    }

    [Theory]
    [InlineData(".NETCoreApp,Version=v10.0", "net10.0")]
    [InlineData(".NETCoreApp,Version=v11.0", "net11.0")]
    public void NetFramework_NamesTheAppsDotnetVersion(string targetFrameworkName, string expected)
    {
        // Act
        var net = WatchiousLive.NetFramework(targetFrameworkName);

        // Assert
        net.ShouldBe(expected);
    }

    private static async Task<Microsoft.AspNetCore.Builder.WebApplication> StartServerAsync(CancellationToken cancellationToken)
    {
        var server = WatchiousLive.Start(CounterApp.CreateWatchiousApp, new WatchiousLiveOptions { Url = "http://127.0.0.1:0" });
        await server.StartAsync(cancellationToken);
        return server;
    }

    private static bool IsPlusOne(UiNode node)
    {
        return node.Type == "Button" && node.Content == "+1";
    }

    private static List<LiveMessage> ReadMessages(byte[] answer)
    {
        var messages = new List<LiveMessage>();
        var offset = 0;
        while (offset < answer.Length)
        {
            var kind = answer[offset];
            var sequence = BinaryPrimitives.ReadInt64LittleEndian(answer.AsSpan(offset + 1, 8));
            var length = BinaryPrimitives.ReadInt32LittleEndian(answer.AsSpan(offset + 9, 4));
            messages.Add(new LiveMessage(kind, sequence, answer.AsSpan(offset + 13, length).ToArray()));
            offset += 13 + length;
        }

        return messages;
    }

    private readonly record struct LiveMessage(byte Kind, long Sequence, byte[] Bytes);

    // Does what the live hosts do: applies each batch once, confirms it, and tells the server what it got.
    private sealed class FakeLiveHost(HttpClient http, string session)
    {
        public HostTree Mirror { get; } = new();

        public long LastSequence { get; private set; }

        public Task<HttpResponseMessage> ClickAsync(UiNode node, CancellationToken cancellationToken)
        {
            return http.PostAsJsonAsync($"/live/events?session={session}", new { id = node.Events["click"], name = "click", value = (string?)null }, cancellationToken);
        }

        public async Task ReceiveUntilAsync(Func<bool> isDone, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!isDone())
            {
                DateTime.UtcNow.ShouldBeLessThan(deadline, "The app's patches didn't arrive.");
                var answer = await http.GetByteArrayAsync($"/live/messages?session={session}&ack={LastSequence}", cancellationToken);
                foreach (var message in ReadMessages(answer).Where(message => message.Sequence > LastSequence))
                {
                    LastSequence = message.Sequence;
                    message.Kind.ShouldBe((byte)1, Encoding.UTF8.GetString(message.Bytes));
                    Mirror.Apply(Patch.ReadAll(message.Bytes));
                    await http.PostAsJsonAsync($"/live/events?session={session}", new { applied = true }, cancellationToken);
                }
            }
        }
    }
}
