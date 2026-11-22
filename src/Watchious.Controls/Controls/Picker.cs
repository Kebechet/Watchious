using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// A wheel of options the crown turns: SwiftUI's Picker on watchOS, Wear OS's Picker.
// <Picker Text="Reps" Items="@reps" @bind-SelectedIndex="selected" />
public sealed class Picker : ComponentBase
{
    // Sent as one attribute with the items on separate lines, so an item's own line breaks become spaces.
    private const char ItemSeparator = '\n';

    // The picker's label, which screen readers also read.
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public IReadOnlyList<string> Items { get; set; } = [];

    [Parameter]
    public int SelectedIndex { get; set; }

    [Parameter]
    public EventCallback<int> SelectedIndexChanged { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Picker));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(Items), string.Join(ItemSeparator, Items.Select(item => item.ReplaceLineEndings(" "))));
        builder.AddAttribute(3, nameof(SelectedIndex), ProtocolValue.Of(SelectedIndex));
        builder.AddAttribute(4, "onselected", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeSelectedAsync));
        builder.CloseElement();
    }

    private Task OnNativeSelectedAsync(NativeEventArgs args)
    {
        if (!ProtocolValue.TryParseInt(args.Value, out var index) || index < 0 || index >= Items.Count)
        {
            return Task.CompletedTask;
        }

        SelectedIndex = index;
        return SelectedIndexChanged.InvokeAsync(index);
    }
}
