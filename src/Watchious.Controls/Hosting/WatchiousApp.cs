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
    private InspectorHost? _inspector;

    internal WatchiousApp(ServiceProvider services, NavigationService navigation)
    {
        _services = services;
        _navigation = navigation;
    }

    public IServiceProvider Services => _services;

    // The ws:// address of a Watchious inspector to mirror the app to. The platform hosts set it from the app's
    // launch arguments (--watchious-inspector=...), on Wear OS only in debuggable builds. Set before StartAsync.
    public Uri? InspectorUrl { get; set; }

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

        if (InspectorUrl is not null)
        {
            _inspector = new InspectorHost(host, InspectorUrl, DispatchEventAsync);
            host = _inspector;
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

        _inspector?.ReportEvent(eventHandlerId, eventName, value);
        return _renderer.DispatchNativeEventAsync(eventHandlerId, eventName, value);
    }

    public async ValueTask DisposeAsync()
    {
        if (_renderer is not null)
        {
            await _renderer.DisposeAsync();
            _renderer = null;
        }

        if (_inspector is not null)
        {
            await _inspector.DisposeAsync();
            _inspector = null;
        }

        await _services.DisposeAsync();
    }
}
