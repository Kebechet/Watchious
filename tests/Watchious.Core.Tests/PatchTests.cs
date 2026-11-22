using System.Buffers.Binary;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Core.Tests.Pages;
using Watchious.Hosting;
using Watchious.Protocol;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

public class PatchTests
{
    [Fact]
    public async Task Start_SendsProtocolVersion2()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();

        // Act
        await app.StartAsync(host);

        // Assert
        BinaryPrimitives.ReadInt32LittleEndian(host.RawBatches.ShouldHaveSingleItem()).ShouldBe(2);
    }

    [Fact]
    public async Task ButtonClick_SendsOnlyTheChangedText()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        await app.StartAsync(host);
        var countValue = LabelWithContent(host, "Count: 0").Children.Single(node => node.Text == "0");

        // Act
        await app.DispatchEventAsync(host.Button("+1").Events["click"], "click", null);

        // Assert
        host.LastBatch.ShouldBe([new Patch(PatchOperation.SetText, countValue.Id, 0, 0, 0, null, "1", 0)]);
    }

    [Fact]
    public async Task ToggleOn_InsertsTheConditionalLabelAfterTheToggle()
    {
        // Arrange
        var host = new RecordingHost();
        await using var app = CounterApp.CreateWatchiousApp();
        await app.StartAsync(host);
        var nodeIds = host.LastTree.Descendants().Select(node => node.Id).ToList();

        // Act
        await app.DispatchEventAsync(host.LastTree.Single("Toggle").Events["toggled"], "toggled", "true");

        // Assert
        host.LastBatch.Count(patch => patch.Operation == PatchOperation.CreateElement).ShouldBe(1);
        var types = host.LastTree.Single("VStack").VisibleChildren.Select(node => node.Type).ToList();
        types.Skip(types.IndexOf("Toggle")).Take(2).ShouldBe(["Toggle", "Label"]);
        host.LastTree.Descendants().Select(node => node.Id).Where(nodeIds.Contains).ShouldBe(nodeIds);
    }

    [Fact]
    public async Task Refresh_WithNothingChanged_SendsNoBatch()
    {
        // Arrange
        var (app, host, _) = await StartAsync<KeyedListPage>("a", "b");
        await using var disposeApp = app;

        // Act
        await RefreshAsync(app, host);

        // Assert
        host.Batches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task KeyedList_Insert_CreatesOnlyTheNewItemsAndKeepsTheOthers()
    {
        // Arrange
        var (app, host, state) = await StartAsync<KeyedListPage>("b", "d");
        await using var disposeApp = app;
        var keptIds = new[] { LabelWithContent(host, "b").Id, LabelWithContent(host, "d").Id };
        state.Items = ["a", "b", "c", "d", "e"];

        // Act
        await RefreshAsync(app, host);

        // Assert
        StackContents(host).ShouldBe(["Header", "a", "b", "c", "d", "e", "Refresh"]);
        new[] { LabelWithContent(host, "b").Id, LabelWithContent(host, "d").Id }.ShouldBe(keptIds);
        host.LastBatch.Count(patch => patch.Operation == PatchOperation.CreateElement).ShouldBe(3);
        host.LastBatch.ShouldNotContain(patch => patch.Operation == PatchOperation.RemoveChild);
    }

    [Fact]
    public async Task KeyedList_Remove_RemovesOnlyThatItem()
    {
        // Arrange
        var (app, host, state) = await StartAsync<KeyedListPage>("a", "b", "c");
        await using var disposeApp = app;
        var stack = host.LastTree.Single("VStack");
        var index = stack.Children.IndexOf(LabelWithContent(host, "b"));
        state.Items = ["a", "c"];

        // Act
        await RefreshAsync(app, host);

        // Assert
        host.LastBatch.ShouldBe([new Patch(PatchOperation.RemoveChild, stack.Id, 0, index, 0, null, null, 0)]);
        StackContents(host).ShouldBe(["Header", "a", "c", "Refresh"]);
    }

    [Fact]
    public async Task KeyedList_RemoveComponentWithTwoNodes_RemovesBothNodes()
    {
        // Arrange
        var (app, host, state) = await StartAsync<KeyedListPage>("a", "b!", "c");
        await using var disposeApp = app;
        var stack = host.LastTree.Single("VStack");
        var index = stack.Children.IndexOf(LabelWithContent(host, "b!"));
        state.Items = ["a", "c"];

        // Act
        await RefreshAsync(app, host);

        // Assert
        var removeAtIndex = new Patch(PatchOperation.RemoveChild, stack.Id, 0, index, 0, null, null, 0);
        host.LastBatch.ShouldBe([removeAtIndex, removeAtIndex]);
        StackContents(host).ShouldBe(["Header", "a", "c", "Refresh"]);
        ShouldHaveNoDetachedNodes(host);
    }

    [Fact]
    public async Task KeyedList_Reverse_MovesNodesWithoutRecreatingThem()
    {
        // Arrange
        var (app, host, state) = await StartAsync<KeyedListPage>("a", "b!", "c", "d");
        await using var disposeApp = app;
        var contentsById = ContentsById(host);
        state.Items = ["d", "c", "b!", "a"];

        // Act
        await RefreshAsync(app, host);

        // Assert
        StackContents(host).ShouldBe(["Header", "d", "c", "b!", "(starred)", "a", "Refresh"]);
        host.LastBatch.ShouldAllBe(patch => patch.Operation == PatchOperation.MoveChild);
        ContentsById(host).ShouldBe(contentsById, ignoreOrder: true);
    }

    [Fact]
    public async Task UnkeyedList_Reorder_UpdatesTheTextInPlace()
    {
        // Arrange
        var (app, host, state) = await StartAsync<UnkeyedListPage>("a", "b");
        await using var disposeApp = app;
        state.Items = ["b", "a"];

        // Act
        await RefreshAsync(app, host);

        // Assert
        host.LastBatch.Select(patch => patch.Operation).ShouldBe([PatchOperation.SetText, PatchOperation.SetText]);
        StackContents(host).ShouldBe(["Header", "b", "a", "Refresh"]);
    }

    [Fact]
    public async Task ComponentGrows_InsertsAfterItsOwnNodes()
    {
        // Arrange
        var (app, host, state) = await StartAsync<UnkeyedListPage>("a", "b");
        await using var disposeApp = app;
        var stack = host.LastTree.Single("VStack");
        var index = stack.Children.IndexOf(LabelWithContent(host, "a")) + 1;
        state.Items = ["a!", "b"];

        // Act
        await RefreshAsync(app, host);

        // Assert
        StackContents(host).ShouldBe(["Header", "a!", "(starred)", "b", "Refresh"]);
        host.LastBatch.ShouldContain(patch =>
            patch.Operation == PatchOperation.InsertChild && patch.NodeId == stack.Id && patch.Index == index);
    }

    [Fact]
    public async Task KeyedList_RandomChanges_HostTreeMatchesThePage()
    {
        // Arrange
        var random = new Random(20261121);
        var items = new List<string> { "i0", "i1!", "i2" };
        var (app, host, state) = await StartAsync<KeyedListPage>([.. items]);
        await using var disposeApp = app;
        var nextItem = 3;

        for (var step = 0; step < 300; step++)
        {
            var idsBefore = ItemLabelIds(host);
            switch (random.Next(4))
            {
                case 0:
                    items.Insert(random.Next(items.Count + 1), $"i{nextItem++}" + (random.Next(3) == 0 ? "!" : ""));
                    break;
                case 1 when items.Count > 0:
                    items.RemoveAt(random.Next(items.Count));
                    break;
                case 2 when items.Count > 1:
                    var moved = items[random.Next(items.Count)];
                    items.Remove(moved);
                    items.Insert(random.Next(items.Count + 1), moved);
                    break;
                default:
                    items = [.. items.OrderBy(_ => random.Next())];
                    break;
            }

            state.Items = [.. items];

            // Act
            await RefreshAsync(app, host);

            // Assert
            List<string> expected = [.. items.SelectMany(item => item.EndsWith('!') ? new[] { item, "(starred)" } : [item])];
            StackContents(host).ShouldBe(["Header", .. expected, "Refresh"], $"step {step}");
            foreach (var (item, id) in ItemLabelIds(host).Where(pair => idsBefore.ContainsKey(pair.Key)))
            {
                id.ShouldBe(idsBefore[item], $"step {step}: {item} was recreated");
            }

            ShouldHaveNoDetachedNodes(host);
            host.Errors.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task AttributeAndEvent_RemovedAndAddedAgain()
    {
        // Arrange
        var (app, host, state) = await StartAsync<ChangingPage>(flag: true);
        await using var disposeApp = app;
        var buttons = host.LastTree.Single("VStack").VisibleChildren.Where(node => node.Type == "Button").ToList();
        var labelled = buttons[0];
        var maybe = buttons.Single(node => node.Content == "Maybe");
        labelled.Content.ShouldBe("on");
        maybe.Events.ShouldContainKey("click");

        // Act
        state.Flag = false;
        await RefreshAsync(app, host);
        var removal = host.LastBatch;
        state.Flag = true;
        await RefreshAsync(app, host);

        // Assert
        removal.ShouldBe(
            [
                new Patch(PatchOperation.RemoveAttribute, labelled.Id, 0, 0, 0, "Text", null, 0),
                new Patch(PatchOperation.RemoveEvent, maybe.Id, 0, 0, 0, "click", null, 0),
            ],
            ignoreOrder: true);
        labelled.Content.ShouldBe("on");
        maybe.Events.ShouldContainKey("click");
    }

    [Fact]
    public async Task TextContent_Changes_SetsTheTextOfTheSameNode()
    {
        // Arrange
        var (app, host, state) = await StartAsync<ChangingPage>(text: "one");
        await using var disposeApp = app;
        var text = host.LastTree.Descendants().Single(node => node.Type == UiNode.TextType && node.Text == "one");
        state.Text = "two";

        // Act
        await RefreshAsync(app, host);

        // Assert
        host.LastBatch.ShouldBe([new Patch(PatchOperation.SetText, text.Id, 0, 0, 0, null, "two", 0)]);
        text.Text.ShouldBe("two");
    }

    private static Task<(WatchiousApp App, RecordingHost Host, PageState State)> StartAsync<TPage>(
        params string[] items)
        where TPage : IComponent
    {
        return StartAsync<TPage>(new PageState { Items = [.. items] });
    }

    private static Task<(WatchiousApp App, RecordingHost Host, PageState State)> StartAsync<TPage>(
        bool flag = false,
        string text = "")
        where TPage : IComponent
    {
        return StartAsync<TPage>(new PageState { Flag = flag, Text = text });
    }

    private static async Task<(WatchiousApp App, RecordingHost Host, PageState State)> StartAsync<TPage>(PageState state)
        where TPage : IComponent
    {
        var host = new RecordingHost();
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton(state);
        var app = builder.UseWatchiousApp<TPage>().Build();
        await app.StartAsync(host);
        host.Errors.ShouldBeEmpty();
        return (app, host, state);
    }

    private static Task RefreshAsync(WatchiousApp app, RecordingHost host)
    {
        var refresh = host.LastTree.Descendants().Single(node => node.Type == "Button" && node.Content == "Refresh");
        return app.DispatchEventAsync(refresh.Events["click"], "click", null);
    }

    private static UiNode LabelWithContent(RecordingHost host, string content)
    {
        return host.LastTree.Descendants().Single(node => node.Type == "Label" && node.Content == content);
    }

    private static List<string?> StackContents(RecordingHost host)
    {
        return [.. host.LastTree.Single("VStack").VisibleChildren.Select(node => node.Content)];
    }

    private static Dictionary<uint, string?> ContentsById(RecordingHost host)
    {
        return host.LastTree.Descendants().ToDictionary(node => node.Id, node => node.Content);
    }

    private static Dictionary<string, uint> ItemLabelIds(RecordingHost host)
    {
        return host.LastTree.Single("VStack").VisibleChildren
            .Where(node => node.Type == "Label" && node.Content is not ("Header" or "(starred)"))
            .ToDictionary(node => node.Content!, node => node.Id);
    }

    // Every node the host was told about is in the tree, so removals forget whole subtrees.
    private static void ShouldHaveNoDetachedNodes(RecordingHost host)
    {
        host.Tree.NodeCount.ShouldBe(1 + host.LastTree.Descendants().Count());
    }
}
