using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Watchious.Navigation;

internal sealed class NavigationEntry(
    long id,
    string route,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type pageType,
    IReadOnlyDictionary<string, object?> parameters)
{
    public long Id { get; } = id;

    public string Route { get; } = route;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type PageType { get; } = pageType;

    public IReadOnlyDictionary<string, object?> Parameters { get; } = parameters;
}

internal sealed class NavigationService : INavigation
{
    private static readonly Dictionary<string, object?> NoParameters = [];

    private readonly List<PageRoute> _routes;
    private readonly List<NavigationEntry> _entries = [];
    private long _lastEntryId;
    private Dispatcher? _dispatcher;

    public NavigationService(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type rootPageType,
        IEnumerable<PageRoute> routes)
    {
        // More specific routes first, like Blazor: literal segments before parameters, constrained parameters
        // before unconstrained ones.
        _routes = [.. routes.OrderByDescending(route => route.Template.Precedence)];
        var rootRoute = _routes.FirstOrDefault(route => route.PageType == rootPageType && !route.Template.HasParameters);
        _entries.Add(new NavigationEntry(++_lastEntryId, rootRoute?.Template.Template ?? "/", rootPageType, NoParameters));
    }

    // Raised on the renderer's dispatcher after the stack changed.
    public event Action? Changed;

    // Lets a test check that every subscriber unsubscribed when it was disposed.
    public bool HasChangedSubscribers => Changed is not null;

    public IReadOnlyList<NavigationEntry> Entries => _entries;

    public IReadOnlyList<string> Stack => [.. _entries.Select(entry => entry.Route)];

    public bool CanGoBack => _entries.Count > 1;

    public void Start(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task GoToAsync(string route)
    {
        foreach (var pageRoute in _routes)
        {
            if (pageRoute.Template.TryMatch(route, out var parameters))
            {
                return ChangeAsync(() => _entries.Add(new NavigationEntry(++_lastEntryId, route, pageRoute.PageType, parameters)));
            }
        }

        throw new ArgumentException($"No page has the route \"{route}\".", nameof(route));
    }

    public Task GoBackAsync()
    {
        return ChangeAsync(() => GoBackTo(_entries.Count - 1));
    }

    public Task GoBackToRootAsync()
    {
        return ChangeAsync(() => GoBackTo(1));
    }

    // The user went back natively (back button, swipe) and the host reports how many pages are left.
    public void OnNativeBack(int remainingPageCount)
    {
        GoBackTo(remainingPageCount);
        Changed?.Invoke();
    }

    private void GoBackTo(int pageCount)
    {
        if (pageCount >= 1 && pageCount < _entries.Count)
        {
            _entries.RemoveRange(pageCount, _entries.Count - pageCount);
        }
    }

    private Task ChangeAsync(Action change)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("Start the app before navigating.");
        return dispatcher.InvokeAsync(() =>
        {
            change();
            Changed?.Invoke();
        });
    }
}
