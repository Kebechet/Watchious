using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Watchious.Hosting;

public sealed class WatchiousApp : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private readonly Type _rootComponentType;

    private WatchiousRenderer? _renderer;

    internal WatchiousApp(
        ServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type rootComponentType)
    {
        _services = services;
        _rootComponentType = rootComponentType;
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
        return _renderer.AddRootComponentAsync(_rootComponentType);
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
