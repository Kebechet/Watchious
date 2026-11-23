using Android.App;
using Microsoft.Extensions.DependencyInjection;
using Watchious.Essentials;

[assembly: UsesPermission(Android.Manifest.Permission.Vibrate)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]

namespace Watchious.Hosts.WearOS.Essentials;

internal static class WearOSEssentials
{
    public static void AddTo(IServiceCollection services)
    {
        var context = Application.Context;
        services.AddSingleton<IHaptics>(new WearOSHaptics(context));
        services.AddSingleton<IDeviceInfo>(new WearOSDeviceInfo(context));
        services.AddSingleton<IBattery>(new WearOSBattery(context));
        services.AddSingleton<IConnectivity>(new WearOSConnectivity(context));
        services.AddSingleton<IPreferences>(new WearOSPreferences(context));
    }
}
