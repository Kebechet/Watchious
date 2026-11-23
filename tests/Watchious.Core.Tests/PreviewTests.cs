using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Preview;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

public class PreviewTests
{
    [Fact]
    public async Task Session_SendsPatchesAndDispatchesThePageEvents()
    {
        // Arrange
        var options = new WatchiousPreviewOptions { Url = "http://127.0.0.1:0", OpenBrowser = false };
        await using var server = WatchiousPreview.Start(CounterApp.CreateWatchiousApp, options);
        await server.StartAsync(TestContext.Current.CancellationToken);
        using var socket = new ClientWebSocket();
        var sessionUrl = new Uri(server.Urls.First().Replace("http://", "ws://", StringComparison.Ordinal) + "/session?platform=watchos");
        await socket.ConnectAsync(sessionUrl, TestContext.Current.CancellationToken);
        var tree = new HostTree();
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));

        // Act
        await ClickAsync(socket, tree, "+1");
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));
        await ClickAsync(socket, tree, "Device");
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));

        // Assert
        var contents = tree.Root.Descendants().Select(node => node.Content).ToList();
        contents.ShouldContain("Count: 1");
        contents.ShouldContain("watchOS");
        contents.ShouldContain("Watchious Preview");
        contents.OfType<string>().ShouldContain(content => content.StartsWith("Battery: 80", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Page_ServesTheRendererAndItsStyles()
    {
        // Arrange
        var options = new WatchiousPreviewOptions { Url = "http://127.0.0.1:0", OpenBrowser = false };
        await using var server = WatchiousPreview.Start(CounterApp.CreateWatchiousApp, options);
        await server.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(server.Urls.First()) };

        // Act
        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var script = await client.GetStringAsync("/preview.js", TestContext.Current.CancellationToken);
        var missingImage = await client.GetAsync("/images/..%2Fsecret", TestContext.Current.CancellationToken);

        // Assert
        page.ShouldContain("preview.js");
        script.ShouldContain("applyPatches");
        missingImage.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task Session_ReadsAClickWhileAnEarlierHandlerStillRuns()
    {
        // Arrange
        var options = new WatchiousPreviewOptions { Url = "http://127.0.0.1:0", OpenBrowser = false };
        await using var server = WatchiousPreview.Start(
            () => WatchiousApp.CreateBuilder().UseWatchiousApp<StartStopPage>().Build(),
            options);
        await server.StartAsync(TestContext.Current.CancellationToken);
        using var socket = new ClientWebSocket();
        var sessionUrl = new Uri(server.Urls.First().Replace("http://", "ws://", StringComparison.Ordinal) + "/session");
        await socket.ConnectAsync(sessionUrl, TestContext.Current.CancellationToken);
        var tree = new HostTree();
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));
        await ClickAsync(socket, tree, "Start");
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));

        // Act
        await ClickAsync(socket, tree, "Stop");
        tree.Apply(Patch.ReadAll(await ReceiveAsync(socket)));

        // Assert
        tree.Root.Descendants().Select(node => node.Content).ShouldContain("Stopped");
    }

    [Fact]
    public void Images_AreServedOnlyFromTheirFolder()
    {
        // Arrange
        var folder = Directory.CreateTempSubdirectory("watchious-preview-");
        try
        {
            var images = folder.CreateSubdirectory("Images");
            File.WriteAllBytes(Path.Combine(images.FullName, "logo.png"), [1]);
            File.WriteAllBytes(Path.Combine(folder.FullName, "secret.png"), [2]);

            // Act
            var image = PreviewImages.Find(images.FullName, "logo");
            var outsideImage = PreviewImages.Find(images.FullName, "../secret");

            // Assert
            image.ShouldBeOfType<PhysicalFileHttpResult>();
            outsideImage.ShouldBeOfType<NotFound>();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    private static async Task<byte[]> ReceiveAsync(ClientWebSocket socket)
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

        result.MessageType.ShouldBe(WebSocketMessageType.Binary, Encoding.UTF8.GetString(message.ToArray()));
        return message.ToArray();
    }

    private static Task ClickAsync(ClientWebSocket socket, HostTree tree, string buttonText)
    {
        var button = tree.Root.Descendants().Last(node => node.Type == "Button" && node.Content == buttonText);
        var json = JsonSerializer.SerializeToUtf8Bytes(new { id = button.Events["click"], name = "click" });
        return socket.SendAsync(json, WebSocketMessageType.Text, endOfMessage: true, TestContext.Current.CancellationToken);
    }
}
