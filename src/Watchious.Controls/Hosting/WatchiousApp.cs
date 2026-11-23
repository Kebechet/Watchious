using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Watchious.Navigation;

namespace Watchious.Hosting;

public sealed class WatchiousApp : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly NavigationService _navigation;

    private WatchiousRenderer? _renderer;

    internal WatchiousApp(ServiceProvider services, NavigationService navigation)
    {
        _services = services;
        _navigation = navigation;
    }

    public IServiceProvider Services => _services;

    public static WatchiousAppBuilder CreateBuilder()
    {
        return new WatchiousAppBuilder();
    }

    public Task StartAsync(IWatchiousHost host)
    {
        if (_renderer is not null)
        {
            throw new InvalidOperationException("The app is already started.");
        }

        var loggerFactory = _services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        _renderer = new WatchiousRenderer(_services, loggerFactory, host);
        _navigation.Start(_renderer.Dispatcher);
        return _renderer.AddRootComponentAsync(typeof(WatchiousNavigationRoot));
    }

    public Task DispatchEventAsync(ulong eventHandlerId, string eventName, string? value)
    {
        if (_renderer is null)
        {
            throw new InvalidOperationException("Start the app before dispatching events.");
        }

        return _renderer.DispatchNativeEventAsync(eventHandlerId, eventName, value);
    }

    public async ValueTask DisposeAsync()
    {
        if (_renderer is not null)
        {
            await _renderer.DisposeAsync();
            _renderer = null;
        }

        await _services.DisposeAsync();
    }
}
