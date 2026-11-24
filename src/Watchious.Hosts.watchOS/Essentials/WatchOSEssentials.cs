using Microsoft.Extensions.DependencyInjection;
using Watchious.Essentials;

namespace Watchious.Hosts.watchOS.Essentials;

internal static class WatchOSEssentials
{
    public static void AddTo(IServiceCollection services)
    {
        WatchOSCallbacks.Register();
        services.AddSingleton<IHaptics, WatchOSHaptics>();
        services.AddSingleton<IDeviceInfo, WatchOSDeviceInfo>();
        services.AddSingleton<IBattery, WatchOSBattery>();
        services.AddSingleton<IConnectivity, WatchOSConnectivity>();
        services.AddSingleton<IPreferences, WatchOSPreferences>();
        services.AddSingleton<ISecureStorage, WatchOSSecureStorage>();
        services.AddSingleton<IAppLifecycle, WatchOSAppLifecycle>();
        services.AddSingleton<IAmbientMode, WatchOSAmbientMode>();
        services.AddSingleton<ILocalNotifications, WatchOSLocalNotifications>();
        services.AddSingleton<IGeolocation, WatchOSGeolocation>();
        services.AddSingleton<IAccelerometer, WatchOSAccelerometer>();
        services.AddSingleton<IGyroscope, WatchOSGyroscope>();
        services.AddSingleton<IMagnetometer, WatchOSMagnetometer>();
        services.AddSingleton<ICompass, WatchOSCompass>();
        services.AddSingleton<IBarometer, WatchOSBarometer>();
        services.AddSingleton<IOrientationSensor, WatchOSOrientationSensor>();
    }
}

internal sealed class WatchOSHaptics : IHaptics
{
    // The host maps HapticFeedback's values to WatchKit's haptic types.
    public void Play(HapticFeedback feedback)
    {
        WatchOSNative.HapticsPlay((int)feedback);
    }
}

internal sealed class WatchOSDeviceInfo : IDeviceInfo
{
    private readonly Lazy<string> _model = new(() => WatchOSNative.TakeString(WatchOSNative.DeviceString(WatchOSNative.DeviceModel)) ?? "");
    private readonly Lazy<Version> _osVersion = new(ReadOSVersion);

    public WatchPlatform Platform => WatchPlatform.watchOS;

    public string Manufacturer => "Apple";

    public string Model => _model.Value;

    public Version OSVersion => _osVersion.Value;

    public bool IsRoundScreen => false;

    // watchOS reports "27.2" or "27.2.1".
    private static Version ReadOSVersion()
    {
        var text = WatchOSNative.TakeString(WatchOSNative.DeviceString(WatchOSNative.DeviceSystemVersion)) ?? "";
        return Version.TryParse(text.Contains('.') ? text : text + ".0", out var version) ? version : new Version(0, 0);
    }
}

internal sealed class WatchOSBattery : IBattery
{
    public double? ChargeLevel
    {
        get
        {
            var level = WatchOSNative.BatteryLevel();
            return level < 0 ? null : level;
        }
    }

    public BatteryState State => WatchOSNative.BatteryState() switch
    {
        1 => BatteryState.Charging,
        2 => BatteryState.Discharging,
        3 => BatteryState.Full,
        _ => BatteryState.Unknown,
    };
}

internal sealed class WatchOSConnectivity : IConnectivity
{
    public NetworkAccess NetworkAccess => WatchOSNative.NetworkStatus() switch
    {
        1 => NetworkAccess.None,
        2 => NetworkAccess.Internet,
        _ => NetworkAccess.Unknown,
    };
}

internal sealed class WatchOSPreferences : StringPreferences
{
    public override bool ContainsKey(string key)
    {
        return WatchOSNative.PreferencesContains(key) != 0;
    }

    public override void Remove(string key)
    {
        WatchOSNative.PreferencesRemove(key);
    }

    public override void Clear()
    {
        WatchOSNative.PreferencesClear();
    }

    protected override string? GetString(string key)
    {
        return WatchOSNative.TakeString(WatchOSNative.PreferencesGet(key));
    }

    protected override void SetString(string key, string value)
    {
        WatchOSNative.PreferencesSet(key, value);
    }
}
