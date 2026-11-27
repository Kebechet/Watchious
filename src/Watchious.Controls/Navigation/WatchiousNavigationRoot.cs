using System.ComponentModel;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Watchious.Controls;

namespace Watchious.Navigation;

// The renderer's root: a NavigationStack element with one Page element per page on the stack. Keeping the pages
// below the top one rendered keeps their state, so going back shows them as they were, as on both platforms.
// Each Page element carries the page's background, resolved from the page (when it is an IPage) and the theme; a
// change to either renders the root again.
internal sealed class WatchiousNavigationRoot : ComponentBase, IDisposable
{
    private static readonly HashSet<string> AppearanceProperties =
    [
        nameof(IPage.Background), nameof(IPage.BackgroundColor),
        nameof(ITheme.PageBackground), nameof(ITheme.PageBackgroundColor),
    ];

    // The pages that are IPage, by navigation entry, whose PropertyChanged this root follows while they are open.
    private readonly Dictionary<long, IPage> _pages = [];

    [Inject]
    private NavigationService Navigation { get; set; } = default!;

    [Inject]
    private ITheme Theme { get; set; } = default!;

    public void Dispose()
    {
        Navigation.Changed -= OnNavigationChanged;
        Theme.PropertyChanged -= OnAppearanceChanged;
        foreach (var page in _pages.Values)
        {
            page.PropertyChanged -= OnAppearanceChanged;
        }

        _pages.Clear();
    }

    protected override void OnInitialized()
    {
        Navigation.Changed += OnNavigationChanged;
        Theme.PropertyChanged += OnAppearanceChanged;
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
            builder.AddAttribute(4, "Background", PageBackground.Resolve(_pages.GetValueOrDefault(entry.Id), Theme));
            builder.OpenComponent(5, entry.PageType);
            foreach (var (name, value) in entry.Parameters)
            {
                builder.AddComponentParameter(6, name, value);
            }

            // Called once the page is created and initialized.
            var entryId = entry.Id;
            builder.AddComponentReferenceCapture(7, instance => OnPageCreated(entryId, instance));
            builder.CloseComponent();
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void OnPageCreated(long entryId, object instance)
    {
        if (instance is not IPage page || _pages.GetValueOrDefault(entryId) == page)
        {
            return;
        }

        Forget(entryId);
        _pages[entryId] = page;
        page.PropertyChanged += OnAppearanceChanged;
        // A background the page set in OnInitialized, before this, goes to the host in the same batch as the page.
        if (page.Background is not null || page.BackgroundColor is not null)
        {
            StateHasChanged();
        }
    }

    private void OnNavigationChanged()
    {
        var openEntries = Navigation.Entries.Select(entry => entry.Id).ToHashSet();
        foreach (var entryId in _pages.Keys.Where(entryId => !openEntries.Contains(entryId)).ToList())
        {
            Forget(entryId);
        }

        StateHasChanged();
    }

    private void Forget(long entryId)
    {
        if (_pages.Remove(entryId, out var page))
        {
            page.PropertyChanged -= OnAppearanceChanged;
        }
    }

    // A page or the theme may change on another thread than the renderer's. A page that raises PropertyChanged for its
    // own data doesn't re-render the root; an empty name means every property changed.
    private void OnAppearanceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName) || AppearanceProperties.Contains(args.PropertyName))
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void OnNativeBack(NativeEventArgs args)
    {
        if (int.TryParse(args.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remainingPageCount))
        {
            Navigation.OnNativeBack(remainingPageCount);
        }
    }
}
