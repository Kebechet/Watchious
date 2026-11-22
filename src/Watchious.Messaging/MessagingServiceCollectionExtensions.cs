using Microsoft.Extensions.DependencyInjection;

namespace Watchious.Messaging;

public static class MessagingServiceCollectionExtensions
{
    // builder.Services.AddPhoneMessaging(), then inject IPhoneMessaging.
    public static IServiceCollection AddPhoneMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IPhoneMessaging>(_ => CreatePhoneMessaging());
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
        throw new InvalidOperationException("No phone can be connected here; phone messaging works on a watch.");
    }

    public Task<IReadOnlyDictionary<string, string>> SendMessageWithReplyAsync(
        IReadOnlyDictionary<string, string> message,
        TimeSpan? timeout = null)
    {
        throw new InvalidOperationException("No phone can be connected here; phone messaging works on a watch.");
    }

    public Task UpdateApplicationContextAsync(IReadOnlyDictionary<string, string> context)
    {
        throw new InvalidOperationException("No phone can be connected here; phone messaging works on a watch.");
    }
}
