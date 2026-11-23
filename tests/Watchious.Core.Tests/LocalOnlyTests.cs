using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Hosting;
using Watchious.Preview;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

public class LocalOnlyTests
{
    [Fact]
    public async Task InspectorPage_FromAnotherSite_IsRejected()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = WatchiousInspector.Start(new WatchiousInspectorOptions { Url = "http://127.0.0.1:0", OpenBrowser = false });
        await server.StartAsync(cancellationToken);
        using var page = new ClientWebSocket();
        page.Options.SetRequestHeader("Origin", "https://evil.example");

        // Act
        var connect = () => page.ConnectAsync(new Uri(WebSocketUrl(server.Urls.First()) + "/inspector/page"), cancellationToken);

        // Assert
        await connect.ShouldThrowAsync<WebSocketException>();
    }

    [Fact]
    public async Task InspectorPage_FromItsOwnPage_IsAccepted()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = WatchiousInspector.Start(new WatchiousInspectorOptions { Url = "http://127.0.0.1:0", OpenBrowser = false });
        await server.StartAsync(cancellationToken);
        var url = server.Urls.First();
        using var page = new ClientWebSocket();
        page.Options.SetRequestHeader("Origin", url);

        // Act
        await page.ConnectAsync(new Uri(WebSocketUrl(url) + "/inspector/page"), cancellationToken);

        // Assert
        page.State.ShouldBe(WebSocketState.Open);
    }

    [Fact]
    public async Task PreviewSession_FromAnotherSite_IsRejected()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = WatchiousPreview.Start(CounterApp.CreateWatchiousApp, new WatchiousPreviewOptions { Url = "http://127.0.0.1:0", OpenBrowser = false });
        await server.StartAsync(cancellationToken);
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "https://evil.example");

        // Act
        var connect = () => socket.ConnectAsync(new Uri(WebSocketUrl(server.Urls.First()) + "/session"), cancellationToken);

        // Assert
        await connect.ShouldThrowAsync<WebSocketException>();
    }

    [Fact]
    public async Task Request_ForAnotherSitesName_IsRejected()
    {
        // Arrange: DNS rebinding points evil.example at this computer, so the browser sends that name.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = WatchiousPreview.Start(CounterApp.CreateWatchiousApp, new WatchiousPreviewOptions { Url = "http://127.0.0.1:0", OpenBrowser = false });
        await server.StartAsync(cancellationToken);
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, server.Urls.First() + "/");
        request.Headers.Host = "evil.example";

        // Act
        using var response = await client.SendAsync(request, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task App_WithAnInspectorAddressThatIsNotWebSocket_StillDisposesItsServices()
    {
        // Arrange
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton<DisposalProbe>();
        var app = builder.UseWatchiousApp<CounterPage>().Build();
        var probe = app.Services.GetRequiredService<DisposalProbe>();
        app.InspectorUrl = new Uri("http://127.0.0.1:9/inspector/app");
        await app.StartAsync(new RecordingHost());

        // Act
        await app.DisposeAsync();

        // Assert
        probe.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task PreviewSession_AfterAMalformedEvent_KeepsDispatching()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var server = WatchiousPreview.Start(CounterApp.CreateWatchiousApp, new WatchiousPreviewOptions { Url = "http://127.0.0.1:0", OpenBrowser = false });
        await server.StartAsync(cancellationToken);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(WebSocketUrl(server.Urls.First()) + "/session"), cancellationToken);
        var tree = new HostTree();
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));
        var plusOne = tree.Root.Descendants().Single(node => node.Type == "Button" && node.Content == "+1");

        // Act
        await socket.SendAsync(Encoding.UTF8.GetBytes("""{"id":"x","name":"click"}"""), WebSocketMessageType.Text, true, cancellationToken);
        await socket.SendAsync(Encoding.UTF8.GetBytes($$"""{"id":{{plusOne.Events["click"]}},"name":"click"}"""), WebSocketMessageType.Text, true, cancellationToken);
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));

        // Assert
        tree.Root.Descendants().ShouldContain(node => node.Type == "Label" && node.Content == "Count: 1");
    }

    private static string WebSocketUrl(string url)
    {
        return url.Replace("http://", "ws://", StringComparison.Ordinal);
    }

    private static async Task<byte[]> ReceiveAsync(ClientWebSocket socket)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, timeout.Token);
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    return message.ToArray();
                }

                message.SetLength(0);
            }
        }
    }

    private sealed class DisposalProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
