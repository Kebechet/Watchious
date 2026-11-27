using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// Text input: on watchOS SwiftUI's TextField, which opens the system's dictation, Scribble and keyboard input; on
// Wear OS a text field that brings up the system keyboard.
// <TextField Placeholder="Name" @bind-Text="name" />
public sealed class TextField : ComponentBase
{
    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public EventCallback<string> TextChanged { get; set; }

    [Parameter]
    public LayoutOptions? HorizontalOptions { get; set; }

    [Parameter]
    public LayoutOptions? VerticalOptions { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(TextField));
        builder.AddAttribute(1, nameof(Placeholder), Placeholder);
        builder.AddAttribute(2, nameof(Text), Text ?? "");
        builder.AddAttribute(3, "onchanged", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeChangedAsync));
        builder.AddAttribute(4, nameof(HorizontalOptions), HorizontalOptions?.ToString());
        builder.AddAttribute(5, nameof(VerticalOptions), VerticalOptions?.ToString());
        builder.CloseElement();
    }

    private Task OnNativeChangedAsync(NativeEventArgs args)
    {
        Text = args.Value ?? "";
        return TextChanged.InvokeAsync(Text);
    }
}
