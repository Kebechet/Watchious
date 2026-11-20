using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Watchious.Hosting;

public sealed class WatchiousAppBuilder
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private Type? _rootComponentType;

    internal WatchiousAppBuilder()
    {
    }

    public IServiceCollection Services { get; } = new ServiceCollection();

    public WatchiousAppBuilder UseWatchiousApp<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TApp>()
        where TApp : IComponent
    {
        _rootComponentType = typeof(TApp);
        return this;
    }

    public WatchiousApp Build()
    {
        if (_rootComponentType is null)
        {
            throw new InvalidOperationException("Call UseWatchiousApp<TApp>() before Build().");
        }

        return new WatchiousApp(Services.BuildServiceProvider(), _rootComponentType);
    }
}
