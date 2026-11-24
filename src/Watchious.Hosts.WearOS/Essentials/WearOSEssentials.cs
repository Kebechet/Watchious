using Android.App;
using Microsoft.Extensions.DependencyInjection;
using Watchious.Essentials;

[assembly: UsesPermission(Android.Manifest.Permission.Vibrate)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]

namespace Watchious.Hosts.WearOS.Essentials;

internal static class WearOSEssentials
{
    // Services that listen to the platform are created by the container, which disposes them, and so stops their
    // listening, with the app.
    public static void AddTo(IServiceCollection services, Activity activity, WearOSPermissions permissions)
    {
        var context = Application.Context;
        services.AddSingleton<IHaptics>(new WearOSHaptics(context));
        services.AddSingleton<IDeviceInfo>(new WearOSDeviceInfo(context));
        services.AddSingleton<IBattery>(new WearOSBattery(context));
        services.AddSingleton<IConnectivity>(new WearOSConnectivity(context));
        services.AddSingleton<IPreferences>(new WearOSPreferences(context));
        services.AddSingleton<ISecureStorage>(new WearOSSecureStorage(context));

        var appLifecycle = new WearOSAppLifecycle(activity);
        services.AddSingleton<IAppLifecycle>(appLifecycle);
        services.AddSingleton<IAmbientMode>(_ => new WearOSAmbientMode(activity, appLifecycle));
        services.AddSingleton<ILocalNotifications>(new WearOSLocalNotifications(context, permissions));
        services.AddSingleton<IGeolocation>(_ => new WearOSGeolocation(context, permissions));

        services.AddSingleton<IAccelerometer>(_ => new WearOSAccelerometer(context));
        services.AddSingleton<IGyroscope>(_ => new WearOSGyroscope(context));
        services.AddSingleton<IMagnetometer>(_ => new WearOSMagnetometer(context));
        services.AddSingleton<ICompass>(_ => new WearOSCompass(context));
        services.AddSingleton<IBarometer>(_ => new WearOSBarometer(context));
        services.AddSingleton<IOrientationSensor>(_ => new WearOSOrientationSensor(context));
    }
}
