using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Watchious.Essentials;

namespace Watchious.Preview;

// Stand-ins for the watch APIs, so pages that use them run in the preview.
internal static class PreviewEssentials
{
    // Shared by all page loads, as preferences and secrets outlive an app's restarts on a watch.
    private static readonly PreviewPreferences Preferences = new();
    private static readonly PreviewSecureStorage SecureStorage = new();

    public static void AddTo(IServiceCollection services, PreviewPlatform platform)
    {
        services.AddSingleton<IHaptics, PreviewHaptics>();
        services.AddSingleton<IDeviceInfo>(new PreviewDeviceInfo(platform));
        services.AddSingleton<IBattery, PreviewBattery>();
        services.AddSingleton<IConnectivity, PreviewConnectivity>();
        services.AddSingleton<IPreferences>(Preferences);
        services.AddSingleton<ISecureStorage>(SecureStorage);
        services.AddSingleton<IAppLifecycle, PreviewAppLifecycle>();
        services.AddSingleton<IAmbientMode, PreviewAmbientMode>();
        services.AddSingleton<ILocalNotifications, PreviewLocalNotifications>();
        services.AddSingleton<IGeolocation, PreviewGeolocation>();
        services.AddSingleton<IAccelerometer, PreviewAccelerometer>();
        services.AddSingleton<IGyroscope, PreviewGyroscope>();
        services.AddSingleton<IMagnetometer, PreviewMagnetometer>();
        services.AddSingleton<ICompass, PreviewCompass>();
        services.AddSingleton<IBarometer, PreviewBarometer>();
        services.AddSingleton<IOrientationSensor, PreviewOrientationSensor>();
    }

    private static FeatureNotSupportedException NotInThePreview(string feature)
    {
        return new FeatureNotSupportedException($"The preview has no {feature}; run the app on a watch or an emulator.");
    }

    private sealed class PreviewHaptics : IHaptics
    {
        public void Play(HapticFeedback feedback)
        {
            Console.WriteLine($"Watchious preview: haptic {feedback}");
        }
    }

    private sealed class PreviewDeviceInfo(PreviewPlatform platform) : IDeviceInfo
    {
        public WatchPlatform Platform => platform == PreviewPlatform.watchOS ? WatchPlatform.watchOS : WatchPlatform.WearOS;

        public string Manufacturer => "Watchious";

        public string Model => "Preview";

        public Version OSVersion => Environment.Version;

        public bool IsRoundScreen => platform == PreviewPlatform.WearOS;
    }

    private sealed class PreviewBattery : IBattery
    {
        public double? ChargeLevel => 0.8;

        public BatteryState State => BatteryState.Discharging;
    }

    private sealed class PreviewConnectivity : IConnectivity
    {
        public NetworkAccess NetworkAccess => NetworkAccess.Internet;
    }

    // A page in the browser is always on screen and in use.
    private sealed class PreviewAppLifecycle : IAppLifecycle
    {
        public AppState State => AppState.Active;

        public event EventHandler<AppStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewAmbientMode : IAmbientMode
    {
        public bool IsSupported => false;

        public bool IsAmbient => false;

        public event EventHandler<AmbientModeChangedEventArgs>? AmbientModeChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewSecureStorage : ISecureStorage
    {
        private readonly ConcurrentDictionary<string, string> _values = new();

        public Task<string?> GetAsync(string key)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
        }

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key)
        {
            return _values.TryRemove(key, out _);
        }

        public void RemoveAll()
        {
            _values.Clear();
        }
    }

    private sealed class PreviewLocalNotifications : ILocalNotifications
    {
        public bool IsSupported => false;

        public Task<bool> RequestPermissionAsync()
        {
            return Task.FromResult(false);
        }

        public Task ShowAsync(LocalNotification notification)
        {
            throw NotInThePreview("notifications");
        }

        public void Cancel(int id)
        {
        }

        public void CancelAll()
        {
        }
    }

    private sealed class PreviewGeolocation : IGeolocation
    {
        public bool IsSupported => false;

        public bool IsListeningForeground => false;

        public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed
        {
            add { }
            remove { }
        }

        public Task<Location?> GetLastKnownLocationAsync()
        {
            throw NotInThePreview("location");
        }

        public Task<Location?> GetLocationAsync(GeolocationRequest? request = null, CancellationToken cancellationToken = default)
        {
            throw NotInThePreview("location");
        }

        public Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
        {
            throw NotInThePreview("location");
        }

        public void StopListeningForeground()
        {
        }
    }

    private abstract class PreviewSensor(string name)
    {
        public bool IsSupported => false;

        public bool IsMonitoring => false;

        public void Start(SensorSpeed sensorSpeed)
        {
            throw NotInThePreview(name);
        }

        public void Stop()
        {
        }
    }

    private sealed class PreviewAccelerometer() : PreviewSensor("accelerometer"), IAccelerometer
    {
        public event EventHandler<AccelerometerChangedEventArgs>? ReadingChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewGyroscope() : PreviewSensor("gyroscope"), IGyroscope
    {
        public event EventHandler<GyroscopeChangedEventArgs>? ReadingChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewMagnetometer() : PreviewSensor("magnetometer"), IMagnetometer
    {
        public event EventHandler<MagnetometerChangedEventArgs>? ReadingChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewCompass() : PreviewSensor("compass"), ICompass
    {
        public event EventHandler<CompassChangedEventArgs>? ReadingChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewBarometer() : PreviewSensor("barometer"), IBarometer
    {
        public event EventHandler<BarometerChangedEventArgs>? ReadingChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewOrientationSensor() : PreviewSensor("orientation sensor"), IOrientationSensor
    {
        public event EventHandler<OrientationSensorChangedEventArgs>? ReadingChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class PreviewPreferences : StringPreferences
    {
        private readonly ConcurrentDictionary<string, string> _values = new();

        public override bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

        public override void Remove(string key)
        {
            _values.TryRemove(key, out _);
        }

        public override void Clear()
        {
            _values.Clear();
        }

        protected override string? GetString(string key)
        {
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        protected override void SetString(string key, string value)
        {
            _values[key] = value;
        }
    }
}
