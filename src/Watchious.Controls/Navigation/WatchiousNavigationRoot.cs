using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Watchious.Navigation;

// The renderer's root: a NavigationStack element with one Page element per page on the stack. Keeping the pages
// below the top one rendered keeps their state, so going back shows them as they were, as on both platforms.
internal sealed class WatchiousNavigationRoot : ComponentBase, IDisposable
{
    [Inject]
    private NavigationService Navigation { get; set; } = default!;

    public void Dispose()
    {
        Navigation.Changed -= OnNavigationChanged;
    }

    protected override void OnInitialized()
    {
        Navigation.Changed += OnNavigationChanged;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "NavigationStack");
        builder.AddAttribute(1, "onback", EventCallback.Factory.Create<NativeEventArgs>(this, OnNativeBack));
        foreach (var entry in Navigation.Entries)
        {
            builder.OpenElement(2, "Page");
            builder.SetKey(entry.Id);
            builder.AddAttribute(3, "Route", entry.Route);
            builder.OpenComponent(4, entry.PageType);
            foreach (var (name, value) in entry.Parameters)
            {
                builder.AddComponentParameter(5, name, value);
            }

            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void OnNavigationChanged()
    {
        StateHasChanged();
    }

    private void OnNativeBack(NativeEventArgs args)
    {
        if (int.TryParse(args.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remainingPageCount))
        {
            Navigation.OnNativeBack(remainingPageCount);
        }
    }
}
