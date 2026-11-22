using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// Like SwiftUI: children are centered, and without Spacing each platform uses its default spacing.
public sealed class VStack : ComponentBase
{
    [Parameter]
    public double? Spacing { get; set; }

    [Parameter]
    public HorizontalAlignment Alignment { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(VStack));
        builder.AddAttribute(1, nameof(Spacing), ProtocolValue.Of(Spacing));
        builder.AddAttribute(2, nameof(Alignment), Alignment.ToString());
        builder.AddContent(3, ChildContent);
        builder.CloseElement();
    }
}

public sealed class HStack : ComponentBase
{
    [Parameter]
    public double? Spacing { get; set; }

    [Parameter]
    public VerticalAlignment Alignment { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(HStack));
        builder.AddAttribute(1, nameof(Spacing), ProtocolValue.Of(Spacing));
        builder.AddAttribute(2, nameof(Alignment), Alignment.ToString());
        builder.AddContent(3, ChildContent);
        builder.CloseElement();
    }
}
