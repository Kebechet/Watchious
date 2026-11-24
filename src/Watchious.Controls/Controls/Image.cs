using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Controls;

// An image of the app, from Resources/Images like MAUI: <Image Source="logo" /> shows Resources/Images/logo.png.
// Watchious.Sdk puts the images into each platform's app; their names must be lowercase letters, digits and _,
// starting with a letter, which is what Android requires of resource names.
// Or a system icon: <Image Icon="Icon.Heart" /> is an SF Symbol on watchOS and a Material icon on Wear OS. A platform's
// own name (AppleSymbol, MaterialIcon) wins over Icon there, and an icon wins over Source.
public sealed class Image : ComponentBase
{
    // The file name without its extension.
    [Parameter]
    public string? Source { get; set; }

    [Parameter]
    public Icon? Icon { get; set; }

    // Any SF Symbol by name, such as "figure.hiking", on watchOS.
    [Parameter]
    public string? AppleSymbol { get; set; }

    // Any Material icon of the Rounded style by its Compose name, such as "Hiking", on Wear OS.
    [Parameter]
    public string? MaterialIcon { get; set; }

    [Parameter]
    public double? Width { get; set; }

    [Parameter]
    public double? Height { get; set; }

    // What screen readers say for the image.
    [Parameter]
    public string? Description { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, nameof(Image));
        builder.AddAttribute(1, nameof(Source), Source);
        builder.AddAttribute(2, nameof(Width), ProtocolValue.Of(Width));
        builder.AddAttribute(3, nameof(Height), ProtocolValue.Of(Height));
        builder.AddAttribute(4, nameof(Description), Description);
        builder.AddAttribute(5, nameof(Icon), Icon?.ToString());
        builder.AddAttribute(6, nameof(AppleSymbol), AppleSymbol);
        builder.AddAttribute(7, nameof(MaterialIcon), MaterialIcon);
        builder.CloseElement();
    }
}
