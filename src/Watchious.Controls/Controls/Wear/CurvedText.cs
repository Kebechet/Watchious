using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Wear;

// Text that follows the bottom edge of a round Wear OS screen while its page is shown, wherever it is in the page; the
// top edge is the clock's (TimeText). It shares the bottom with an EdgeButton, so a page has one or the other. Wear OS
// only.
public sealed class CurvedText : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "Wear.CurvedText");
        builder.AddAttribute(1, nameof(Text), Text);
        builder.CloseElement();
    }
}
