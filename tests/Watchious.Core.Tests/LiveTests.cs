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
        await using var server = WatchiousLive.Start(CounterApp.CreateWatchiousApp, new WatchiousLiveOptions { Url = "http://127.0.0.1:0" });
        await server.StartAsync(cancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };
        var mirror = new HostTree();
        var start = await http.PostAsync("/live/start?platform=watchos", content: null, cancellationToken);
        var session = await start.Content.ReadAsStringAsync(cancellationToken);
        await ReceiveUntilAsync(http, session, mirror, () => mirror.Root.Descendants().Any(IsPlusOne), cancellationToken);
        var plusOne = mirror.Root.Descendants().Single(IsPlusOne);

        // Act
        var click = await http.PostAsJsonAsync($"/live/events?session={session}", new { id = plusOne.Events["click"], name = "click", value = (string?)null }, cancellationToken);
        await ReceiveUntilAsync(http, session, mirror, () => mirror.Root.Descendants().Any(node => node.Content == "Count: 1"), cancellationToken);

        // Assert
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        click.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        mirror.Root.Descendants().ShouldContain(node => node.Content == "Count: 1");
    }

    [Fact]
    public async Task Messages_OfAStoppedOrUnknownSession_AreNotFound()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = WatchiousLive.Start(CounterApp.CreateWatchiousApp, new WatchiousLiveOptions { Url = "http://127.0.0.1:0" });
        await server.StartAsync(cancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };
        var start = await http.PostAsync("/live/start?platform=wearos", content: null, cancellationToken);
        var session = await start.Content.ReadAsStringAsync(cancellationToken);

        // Act
        await http.PostAsync($"/live/stop?session={session}", content: null, cancellationToken);
        var stopped = await http.GetAsync($"/live/messages?session={session}", cancellationToken);
        var unknown = await http.GetAsync("/live/messages?session=unknown", cancellationToken);

        // Assert
        stopped.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static bool IsPlusOne(UiNode node)
    {
        return node.Type == "Button" && node.Content == "+1";
    }

    // Applies each batch as a live host does, and confirms it.
    private static async Task ReceiveUntilAsync(HttpClient http, string session, HostTree mirror, Func<bool> isDone, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!isDone())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "The app's patches didn't arrive.");
            var answer = await http.GetByteArrayAsync($"/live/messages?session={session}", cancellationToken);
            var offset = 0;
            while (offset < answer.Length)
            {
                var kind = answer[offset];
                var length = BinaryPrimitives.ReadInt32LittleEndian(answer.AsSpan(offset + 1, 4));
                var bytes = answer.AsSpan(offset + 5, length).ToArray();
                offset += 5 + length;
                kind.ShouldBe((byte)1, Encoding.UTF8.GetString(bytes));
                mirror.Apply(Patch.ReadAll(bytes));
                await http.PostAsJsonAsync($"/live/events?session={session}", new { applied = true }, cancellationToken);
            }
        }
    }
}
