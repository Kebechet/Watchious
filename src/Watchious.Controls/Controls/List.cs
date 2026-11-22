using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// Rows the crown scrolls: SwiftUI's List on watchOS, and on Wear OS a TransformingLazyColumn, which composes only
// the rows on the screen and shapes them to the round display.
public sealed class List : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(List));
        builder.AddContent(1, ChildContent);
        builder.CloseElement();
    }
}
