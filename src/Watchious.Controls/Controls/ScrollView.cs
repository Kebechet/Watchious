using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class ScrollView : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(ScrollView));
        builder.AddContent(1, ChildContent);
        builder.CloseElement();
    }
}
