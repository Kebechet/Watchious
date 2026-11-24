using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Wear;

// The clock along the top of a round Wear OS screen. Every Watchious screen shows it; a TimeText anywhere in a page
// puts its Text before the time while that page is shown. Wear OS only: watchOS keeps its clock in the corner.
public sealed class TimeText : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "Wear.TimeText");
        builder.AddAttribute(1, nameof(Text), Text);
        builder.CloseElement();
    }
}
