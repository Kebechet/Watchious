using Microsoft.Extensions.DependencyInjection;

namespace Watchious.Hosting;

// How a platform host adds its services, such as the watch APIs, to the app it creates:
//     using (WatchiousPlatform.ConfigureServices(AddPlatformServices)) { app = CreateWatchiousApp(); }
// Every builder created inside the using gets them first, so the app's own registrations can replace them. It is
// an AsyncLocal rather than a static, so apps created elsewhere at the same time (tests, a preview) don't get them.
public static class WatchiousPlatform
{
    private static readonly AsyncLocal<Action<IServiceCollection>?> _configureServices = new();

    public static IDisposable ConfigureServices(Action<IServiceCollection> configureServices)
    {
        var previous = _configureServices.Value;
        _configureServices.Value = configureServices;
        return new Scope(previous);
    }

    internal static void AddServices(IServiceCollection services)
    {
        _configureServices.Value?.Invoke(services);
    }

    private sealed class Scope(Action<IServiceCollection>? previous) : IDisposable
    {
        public void Dispose()
        {
            _configureServices.Value = previous;
        }
    }
}
