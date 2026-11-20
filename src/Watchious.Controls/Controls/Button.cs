using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class Button : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public bool IsEnabled { get; set; } = true;

    [Parameter]
    public EventCallback OnClick { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Button));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(IsEnabled), ProtocolValue.Of(IsEnabled));
        builder.AddAttribute(3, "onclick", OnClick);
        builder.CloseElement();
    }
}
