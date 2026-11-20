using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class Label : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public TextStyle Style { get; set; } = TextStyle.Body;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Label));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(Style), Style.ToString());
        builder.CloseElement();
    }
}
