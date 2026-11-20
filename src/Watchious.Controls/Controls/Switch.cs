using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

public sealed class Switch : ComponentBase
{
    [Parameter]
    public bool IsToggled { get; set; }

    [Parameter]
    public EventCallback<bool> IsToggledChanged { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Switch));
        builder.AddAttribute(1, nameof(IsToggled), ProtocolValue.Of(IsToggled));
        builder.AddAttribute(2, "ontoggled", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeToggledAsync));
        builder.CloseElement();
    }

    private Task OnNativeToggledAsync(NativeEventArgs args)
    {
        IsToggled = ProtocolValue.ToBool(args.Value);
        return IsToggledChanged.InvokeAsync(IsToggled);
    }
}
