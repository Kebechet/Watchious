namespace Watchious.Essentials;

public interface IDeviceInfo
{
    WatchPlatform Platform { get; }

    string Manufacturer { get; }

    // The hardware model, for example "Watch7,1" or "SM-L310".
    string Model { get; }

    Version OSVersion { get; }

    bool IsRoundScreen { get; }
}

// The platform names keep their brands' casing, like MAUI's DevicePlatform.watchOS.
public enum WatchPlatform
{
    WearOS,
    watchOS,
}
