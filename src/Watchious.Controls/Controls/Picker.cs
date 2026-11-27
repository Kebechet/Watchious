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

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Picker));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(Items), string.Join(ItemSeparator, Items.Select(item => item.ReplaceLineEndings(" "))));
        builder.AddAttribute(3, nameof(SelectedIndex), ProtocolValue.Of(SelectedIndex));
        builder.AddAttribute(4, "onselected", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeSelectedAsync));
        builder.AddAttribute(5, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(6, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.CloseElement();
    }

    private Task OnNativeSelectedAsync(NativeEventArgs args)
    {
        if (!ProtocolValue.TryParseInt(args.Value, out var index) || index < 0 || index >= Items.Count)
        {
            return Task.CompletedTask;
        }

        // The page decides, as with Toggle: a page that refuses the new index keeps the picker where it was.
        return SelectedIndexChanged.InvokeAsync(index);
    }
}
