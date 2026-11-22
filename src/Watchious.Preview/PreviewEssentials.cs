using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Watchious.Essentials;

namespace Watchious.Preview;

// Stand-ins for the watch APIs, so pages that use them run in the preview.
internal static class PreviewEssentials
{
    // Shared by all page loads, as preferences outlive an app's restarts on a watch.
    private static readonly PreviewPreferences Preferences = new();

    public static void AddTo(IServiceCollection services, PreviewPlatform platform)
    {
        services.AddSingleton<IHaptics, PreviewHaptics>();
        services.AddSingleton<IDeviceInfo>(new PreviewDeviceInfo(platform));
        services.AddSingleton<IBattery, PreviewBattery>();
        services.AddSingleton<IConnectivity, PreviewConnectivity>();
        services.AddSingleton<IPreferences>(Preferences);
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
