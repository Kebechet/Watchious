using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class Switch : ComponentBase
{
    // Not in MAUI's Switch. Both native toggles (SwiftUI Toggle, Wear OS SwitchButton) carry their own label,
    // which looks native and is read together with the switch by VoiceOver and TalkBack.
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public bool IsToggled { get; set; }

    [Parameter]
    public EventCallback<bool> IsToggledChanged { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Switch));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(IsToggled), ProtocolValue.Of(IsToggled));
        builder.AddAttribute(3, "ontoggled", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeToggledAsync));
        builder.CloseElement();
    }

    private Task OnNativeToggledAsync(NativeEventArgs args)
    {
        IsToggled = ProtocolValue.ToBool(args.Value);
        return IsToggledChanged.InvokeAsync(IsToggled);
    }
}
