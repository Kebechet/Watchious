using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Watchious.Messaging;

public static class MessagingServiceCollectionExtensions
{
    // builder.Services.AddPhoneMessaging(), then inject IPhoneMessaging.
    public static IServiceCollection AddPhoneMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IPhoneMessaging>(_ => CreatePhoneMessaging());
        // Started with the app, not when a page first asks for it: WatchConnectivity and the Data Layer deliver only to
        // a session that listens, and the phone sees the watch app as reachable only then.
        services.AddHostedService<PhoneMessagingStartup>();
        return services;
    }

    private static IPhoneMessaging CreatePhoneMessaging()
    {
#if ANDROID
        return WearOS.WearOSDataLayerChannel.CreateMessaging();
#else
        // The watchOS build runs .NET for iOS's runtime, which reports iOS; the preview and tests run on a desktop.
        return OperatingSystem.IsIOS() ? new WatchConnectivityPhoneMessaging() : new UnavailablePhoneMessaging();
#endif
    }
}

// Creating the service starts it: WatchConnectivity's session, or the Data Layer's listeners.
internal sealed class PhoneMessagingStartup(IServiceProvider services) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        services.GetRequiredService<IPhoneMessaging>();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

// Where no phone can be connected, such as the desktop preview.
internal sealed class UnavailablePhoneMessaging : IPhoneMessaging
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    public event EventHandler<PhoneMessageReceivedEventArgs>? MessageReceived
    {
        add { }
        remove { }
    }

    public event EventHandler? ApplicationContextChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyDictionary<string, string> ReceivedApplicationContext => Empty;

    public Task<bool> IsPhoneReachableAsync()
    {
        return Task.FromResult(false);
    }

    public Task SendMessageAsync(IReadOnlyDictionary<string, string> message)
    {
        return Task.FromException(NoPhone());
    }

    public Task<IReadOnlyDictionary<string, string>> SendMessageWithReplyAsync(
        IReadOnlyDictionary<string, string> message,
        TimeSpan? timeout = null)
    {
        return Task.FromException<IReadOnlyDictionary<string, string>>(NoPhone());
    }

    public Task UpdateApplicationContextAsync(IReadOnlyDictionary<string, string> context)
    {
        return Task.FromException(NoPhone());
    }

    private static InvalidOperationException NoPhone()
    {
        return new InvalidOperationException("No phone can be connected here; phone messaging works on a watch.");
    }
}
