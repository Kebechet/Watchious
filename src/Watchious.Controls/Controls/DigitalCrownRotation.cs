using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// SwiftUI's digitalCrownRotation modifier as a wrapper: the crown turns Value from From through Through in steps of By,
// and the content shows it. On Wear OS the crown or the rotating bezel turns it, one step per detent. On a screen that
// scrolls, the crown scrolls until the user touches the content, as with a Picker.
public sealed class DigitalCrownRotation : ComponentBase
{
    [Parameter]
    public double Value { get; set; }

    [Parameter]
    public EventCallback<double> ValueChanged { get; set; }

    [Parameter, EditorRequired]
    public double From { get; set; }

    [Parameter, EditorRequired]
    public double Through { get; set; }

    [Parameter]
    public double By { get; set; } = 1;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void OnParametersSet()
    {
        if (!(By > 0) || double.IsInfinity(By))
        {
            throw new ArgumentOutOfRangeException(nameof(By), By, "By must be a positive number.");
        }

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
        builder.OpenElement(0, nameof(DigitalCrownRotation));
        builder.AddAttribute(1, nameof(Value), ProtocolValue.Of(Value));
        builder.AddAttribute(2, nameof(From), ProtocolValue.Of(From));
        builder.AddAttribute(3, nameof(Through), ProtocolValue.Of(Through));
        builder.AddAttribute(4, nameof(By), ProtocolValue.Of(By));
        builder.AddAttribute(5, "onchanged", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeChangedAsync));
        builder.AddAttribute(6, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(7, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.AddContent(8, ChildContent);
        builder.CloseElement();
    }

    private Task OnNativeChangedAsync(NativeEventArgs args)
    {
        if (!ProtocolValue.TryParseDouble(args.Value, out var value))
        {
            return Task.CompletedTask;
        }

        // The page decides, as with Toggle: a page that refuses the new value keeps the old one.
        return ValueChanged.InvokeAsync(Math.Clamp(value, From, Through));
    }
}
