using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class Toggle : ComponentBase
{
    // Unlike SwiftUI's Toggle, the label is a parameter rather than content: both native toggles (SwiftUI Toggle,
    // Wear OS SwitchButton) carry their own label, which looks native and is read together with the toggle by
    // VoiceOver and TalkBack.
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public bool IsOn { get; set; }

    [Parameter]
    public EventCallback<bool> IsOnChanged { get; set; }

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Toggle));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(IsOn), ProtocolValue.Of(IsOn));
        builder.AddAttribute(3, "ontoggled", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeToggledAsync));
        builder.AddAttribute(4, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(5, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.CloseElement();
    }

    // The page decides: with @bind-IsOn it takes the new value, and a page that refuses it (because what the toggle
    // starts failed) keeps the toggle as it was. Setting IsOn here would stick, as Blazor doesn't hand the page's
    // unchanged value back to the toggle.
    private Task OnNativeToggledAsync(NativeEventArgs args)
    {
        return IsOnChanged.InvokeAsync(ProtocolValue.ToBool(args.Value));
    }
}
