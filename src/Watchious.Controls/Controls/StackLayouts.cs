using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public abstract class StackLayout : ComponentBase
{
    [Parameter]
    public double Spacing { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected abstract string ElementName { get; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, ElementName);
        builder.AddAttribute(1, nameof(Spacing), ProtocolValue.Of(Spacing));
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}

public sealed class VerticalStackLayout : StackLayout
{
    protected override string ElementName => nameof(VerticalStackLayout);
}

public sealed class HorizontalStackLayout : StackLayout
{
    protected override string ElementName => nameof(HorizontalStackLayout);
}
