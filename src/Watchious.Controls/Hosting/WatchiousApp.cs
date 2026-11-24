using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    private IHostedService[] _hostedServices = [];

    internal WatchiousApp(ServiceProvider services, NavigationService navigation)
    {
        _services = services;
        _navigation = navigation;
    }

    public IServiceProvider Services => _services;

    // The http:// address of a Watchious inspector to mirror the app to. The platform hosts set it: watchOS from the
    // launch argument --watchious-inspector=..., Wear OS from the watchious_inspector intent extra in debuggable
    // builds only. Set before StartAsync.
    public Uri? InspectorUrl { get; set; }

    public static WatchiousAppBuilder CreateBuilder()
    {
        return new WatchiousAppBuilder();
    }

    public async Task StartAsync(IWatchiousHost host)
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

        // Services added with AddHostedService, as in an ASP.NET Core app, start with the app, before its first page,
        // such as Watchious.Messaging's connection to the phone, which has to listen before any page asks for it.
        _hostedServices = [.. _services.GetServices<IHostedService>()];
        foreach (var hostedService in _hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        await _renderer.AddRootComponentAsync(typeof(WatchiousNavigationRoot));
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
        try
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

            for (var index = _hostedServices.Length - 1; index >= 0; index--)
            {
                await _hostedServices[index].StopAsync(CancellationToken.None);
            }

            _hostedServices = [];
        }
        finally
        {
            await _services.DisposeAsync();
        }
    }
}
