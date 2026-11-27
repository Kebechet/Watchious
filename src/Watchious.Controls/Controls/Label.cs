using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// SwiftUI's Text, under MAUI's name: Razor reserves <text> (in any casing) inside @if and @foreach blocks, where
// <Text> would turn into plain markup or fail to compile (by design: https://github.com/dotnet/razor/issues/12620).
// <Label Style="TextStyle.Title">Count: @count</Label>. The content can mix literal text and values; the host joins
// it into one native text, so a value change updates just that part.
public sealed class Label : ComponentBase
{
    [Parameter]
    public TextStyle Style { get; set; } = TextStyle.Body;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Label));
        builder.AddAttribute(1, nameof(Style), Style.ToString());
        builder.AddAttribute(2, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(3, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}
