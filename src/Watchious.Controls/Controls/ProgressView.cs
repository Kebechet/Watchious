using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class ProgressView : ComponentBase
{
    // From 0 to 1. Without a value the progress is indeterminate, shown as a spinner.
    [Parameter]
    public double? Value { get; set; }

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public ProgressViewStyle Style { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(ProgressView));
        builder.AddAttribute(1, nameof(Value), ProtocolValue.Of(Value is null ? null : Math.Clamp(Value.Value, 0, 1)));
        builder.AddAttribute(2, nameof(Text), Text);
        builder.AddAttribute(3, nameof(Style), Style.ToString());
        builder.CloseElement();
    }
}

public enum ProgressViewStyle
{
    // Each platform's own: SwiftUI shows a bar for progress with a value, Wear OS a ring.
    Automatic,
    Linear,
    Circular,
}
