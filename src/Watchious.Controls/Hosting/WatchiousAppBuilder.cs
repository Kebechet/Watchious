using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Watchious.Controls;
using Watchious.Navigation;

namespace Watchious.Hosting;

public sealed class WatchiousAppBuilder
{
    private readonly List<PageRoute> _routes = [];
    private readonly WatchiousTheme _theme = new();

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private Type? _rootComponentType;

    internal WatchiousAppBuilder()
    {
        WatchiousPlatform.AddServices(Services);
    }

    public IServiceCollection Services { get; } = new ServiceCollection();

    // The root page.
    public WatchiousAppBuilder UseWatchiousApp<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TApp>()
        where TApp : IComponent
    {
        _rootComponentType = typeof(TApp);
        return this;
    }

    // The @page directives of the root page's assembly are added by Watchious's source generator. This adds a route
    // by hand, for example for a page in another assembly.
    public WatchiousAppBuilder AddRoute<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TPage>(string template)
        where TPage : IComponent
    {
        _routes.Add(new PageRoute(RouteTemplate.Parse(template), typeof(TPage)));
        return this;
    }

    // The app's defaults, such as the background of every page that sets none (WatchiousTheme). An app that registers
    // its own ITheme in Services gets that one instead, and this one goes unused.
    public WatchiousAppBuilder ConfigureTheme(Action<WatchiousTheme> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_theme);
        return this;
    }

    public WatchiousApp Build()
    {
        if (_rootComponentType is null)
        {
            throw new InvalidOperationException("Call UseWatchiousApp<TApp>() before Build().");
        }

        AddGeneratedRoutes(_rootComponentType.Assembly);
        var navigation = new NavigationService(_rootComponentType, _routes);
        Services.TryAddSingleton(_theme);
        Services.TryAddSingleton<ITheme>(services => services.GetRequiredService<WatchiousTheme>());
        Services.AddSingleton(navigation);
        Services.AddSingleton<INavigation>(navigation);
        return new WatchiousApp(Services.BuildServiceProvider(), navigation);
    }

    private void AddGeneratedRoutes(Assembly assembly)
    {
        foreach (var attribute in assembly.GetCustomAttributes<WatchiousRoutesAttribute>())
        {
            var routes = (IWatchiousRoutes)Activator.CreateInstance(attribute.RoutesType)!;
            routes.AddTo(this);
        }
    }
}
