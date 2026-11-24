using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Watchious.Controls;

namespace Wear;

// The main action of a Wear OS screen: a button that hugs the bottom edge of the round screen while its page is shown,
// wherever it is in the page, with the page's list scrolling above it. It shows an icon or a short text. Wear OS only,
// so a watchOS app shows the same action its own way.
public sealed class EdgeButton : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    // Wins over Text.
    [Parameter]
    public Icon? Icon { get; set; }

    [Parameter]
    public bool IsEnabled { get; set; } = true;

    [Parameter]
    public EventCallback OnClick { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "Wear.EdgeButton");
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(Icon), Icon?.ToString());
        builder.AddAttribute(3, nameof(IsEnabled), ProtocolValue.Of(IsEnabled));
        builder.AddAttribute(4, "onclick", OnClick);
        builder.CloseElement();
    }
}
