using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Shouldly;
using Watchious.Preview;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

public class InspectorTests
{
    [Fact]
    public async Task Inspector_MirrorsTheAppAndDispatchesClicksFromThePage()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new WatchiousInspectorOptions { Url = "http://127.0.0.1:0", OpenBrowser = false };
        await using var server = WatchiousInspector.Start(options);
        await server.StartAsync(cancellationToken);
        var baseUrl = server.Urls.First();

        var nativeHost = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        app.InspectorUrl = new Uri(baseUrl + "/inspector/app");
        await app.StartAsync(nativeHost);

        using var page = new ClientWebSocket();
        await page.ConnectAsync(new Uri(PageUrl(baseUrl)), cancellationToken);
        var mirror = new HostTree();

        // Act
        var firstBatch = await ReceiveUntilBinaryAsync(page, mirror);
        var plusOne = mirror.Root.Descendants().Single(node => node.Type == "Button" && node.Content == "+1");
        var click = JsonSerializer.SerializeToUtf8Bytes(new { id = plusOne.Events["click"], name = "click", value = (string?)null });
        await page.SendAsync(click, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        var reportedEvent = await ReceiveTextAsync(page, mirror);
        await ReceiveUntilBinaryAsync(page, mirror);

        // Assert
        firstBatch.ShouldBeTrue();
        reportedEvent.ShouldContain("\"name\":\"click\"");
        mirror.Root.Descendants().ShouldContain(node => node.Content == "Count: 1");
        nativeHost.LastTree.Descendants().ShouldContain(node => node.Content == "Count: 1");
    }

    [Fact]
    public async Task App_WithoutAnInspectorListening_RunsOn()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        app.InspectorUrl = new Uri("http://127.0.0.1:9/inspector/app");

        // Act
        await app.StartAsync(host);
        await app.DispatchEventAsync(host.Button("+1").Events["click"], "click", null);

        // Assert
        host.LastTree.Descendants().ShouldContain(node => node.Content == "Count: 1");
        host.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task App_WhenDisposed_LeavesThePageWaiting()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new WatchiousInspectorOptions { Url = "http://127.0.0.1:0", OpenBrowser = false };
        await using var server = WatchiousInspector.Start(options);
        await server.StartAsync(cancellationToken);
        var baseUrl = server.Urls.First();
        var app = CounterApp.CreateWatchiousApp();
        app.InspectorUrl = new Uri(baseUrl + "/inspector/app");
        await app.StartAsync(new RecordingHost());
        using var page = new ClientWebSocket();
        await page.ConnectAsync(new Uri(PageUrl(baseUrl)), cancellationToken);
        await ReceiveUntilBinaryAsync(page, new HostTree());

        // Act
        await app.DisposeAsync();
        var status = await ReceiveTextAsync(page, new HostTree(), includeStatus: true);

        // Assert
        status.ShouldBe("""{"status":"waiting"}""");
    }

    private static string PageUrl(string baseUrl)
    {
        return baseUrl.Replace("http://", "ws://", StringComparison.Ordinal) + "/inspector/page";
    }

    private static async Task<bool> ReceiveUntilBinaryAsync(ClientWebSocket socket, HostTree mirror)
    {
        while (true)
        {
            var (bytes, type) = await ReceiveAsync(socket);
            if (type == WebSocketMessageType.Binary)
            {
                mirror.Apply(Patch.ReadAll(bytes));
                return true;
            }
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, HostTree mirror, bool includeStatus = false)
    {
        while (true)
        {
            var (bytes, type) = await ReceiveAsync(socket);
            if (type == WebSocketMessageType.Binary)
            {
                mirror.Apply(Patch.ReadAll(bytes));
                continue;
            }

            var text = Encoding.UTF8.GetString(bytes);
            if (includeStatus || !text.Contains("\"status\"", StringComparison.Ordinal))
            {
                return text;
            }
        }
    }

    private static async Task<(byte[] Bytes, WebSocketMessageType Type)> ReceiveAsync(ClientWebSocket socket)
    {
        using var message = new MemoryStream();
        var buffer = new byte[8192];
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return (message.ToArray(), result.MessageType);
    }
}
