using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// Like SwiftUI: children are centered unless their HorizontalOptions (VStack) or VerticalOptions (HStack) say
// otherwise, and without Spacing each platform uses its default spacing.
public sealed class VStack : ComponentBase
{
    [Parameter]
    public double? Spacing { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(VStack));
        builder.AddAttribute(1, nameof(Spacing), ProtocolValue.Of(Spacing));
        builder.AddAttribute(2, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(3, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}

public sealed class HStack : ComponentBase
{
    [Parameter]
    public double? Spacing { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(HStack));
        builder.AddAttribute(1, nameof(Spacing), ProtocolValue.Of(Spacing));
        builder.AddAttribute(2, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(3, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
    }
}
