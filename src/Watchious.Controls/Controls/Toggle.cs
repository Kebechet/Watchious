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

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Toggle));
        builder.AddAttribute(1, nameof(Text), Text);
        builder.AddAttribute(2, nameof(IsOn), ProtocolValue.Of(IsOn));
        builder.AddAttribute(3, "ontoggled", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeToggledAsync));
        builder.CloseElement();
    }

    private Task OnNativeToggledAsync(NativeEventArgs args)
    {
        IsOn = ProtocolValue.ToBool(args.Value);
        return IsOnChanged.InvokeAsync(IsOn);
    }
}
