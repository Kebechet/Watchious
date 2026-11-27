using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Watchious.Controls;

// Platform-only controls live in the top-level namespaces Apple and Wear, so that markup names the platform: Razor
// finds <Apple.Gauge> only by its full name, and a Watchious.Controls.Apple namespace would make it
// <Watchious.Controls.Apple.Gauge>. On the other platform these controls show nothing.
namespace Apple;

// SwiftUI's Gauge: a value in a range, shown as a ring or a bar, with optional labels. watchOS only.
public sealed class Gauge : ComponentBase
{
    [Parameter]
    public double Value { get; set; }

    [Parameter]
    public double From { get; set; }

    [Parameter]
    public double Through { get; set; } = 1;

    // What the gauge measures; screen readers say it, and the linear styles show it.
    [Parameter]
    public string? Text { get; set; }

    // Shown in the middle of a ring, or next to a bar.
    [Parameter]
    public string? CurrentValueText { get; set; }

    [Parameter]
    public string? MinimumText { get; set; }

    [Parameter]
    public string? MaximumText { get; set; }

    [Parameter]
    public GaugeStyle Style { get; set; }

    protected override void OnParametersSet()
    {
        if (!(From <= Through))
        {
            throw new ArgumentOutOfRangeException(nameof(Through), Through, $"Through must not be less than From ({From}).");
        }
    }

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "Apple.Gauge");
        builder.AddAttribute(1, nameof(Value), ProtocolValue.Of(double.IsNaN(Value) ? From : Math.Clamp(Value, From, Through)));
        builder.AddAttribute(2, nameof(From), ProtocolValue.Of(From));
        builder.AddAttribute(3, nameof(Through), ProtocolValue.Of(Through));
        builder.AddAttribute(4, nameof(Text), Text);
        builder.AddAttribute(5, nameof(CurrentValueText), CurrentValueText);
        builder.AddAttribute(6, nameof(MinimumText), MinimumText);
        builder.AddAttribute(7, nameof(MaximumText), MaximumText);
        builder.AddAttribute(8, nameof(Style), Style.ToString());
        builder.AddAttribute(9, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(10, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.CloseElement();
    }
}
