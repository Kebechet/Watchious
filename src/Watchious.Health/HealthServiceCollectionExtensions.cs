using Kebechet.Types.Health.Enums;
using Kebechet.Types.Health.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Watchious.Health;

public static class HealthServiceCollectionExtensions
{
    // builder.Services.AddWatchiousHealth(), then inject IHealthService. watchOS needs the HealthKit entitlement
    // (Platforms/watchOS/Entitlements.plist) and NSHealthShareUsageDescription / NSHealthUpdateUsageDescription in
    // Platforms/watchOS/Info.plist.
    public static IServiceCollection AddWatchiousHealth(this IServiceCollection services)
    {
        services.AddSingleton<IHealthService>(_ => new HealthService(CreateBackend()));
        return services;
    }

    private static IHealthBackend CreateBackend()
    {
#if ANDROID
        return new WearOS.WearOSHealthBackend();
#else
        // The watchOS build runs .NET for iOS's runtime, which reports iOS; the preview and tests run on a desktop.
        return OperatingSystem.IsIOS() ? new HealthKitBackend() : new UnavailableHealthBackend();
#endif
    }
}

// Where no health sensors exist, such as the desktop preview.
internal sealed class UnavailableHealthBackend : IHealthBackend
{
    public bool IsSupported => false;

    public HealthDataSdk DataSdk => HealthDataSdk.Unknown;

    public string? DataOrigin => null;

    public Action<double, DateTimeOffset>? HeartRateReceived { get; set; }

    public Action? WorkoutEnded { get; set; }

    public Task<IList<string>> RequestPermissionsAsync(IList<HealthPermissionDto> permissions, CancellationToken cancellationToken)
    {
        return Task.FromResult<IList<string>>([.. permissions.Select(permission => permission.HealthDataType.ToString())]);
    }

    public Task StartWorkoutAsync(ActivityType activityType)
    {
        throw new PlatformNotSupportedException("Workouts need a watch's health sensors.");
    }

    public Task PauseWorkoutAsync()
    {
        throw new PlatformNotSupportedException("Workouts need a watch's health sensors.");
    }

    public Task ResumeWorkoutAsync()
    {
        throw new PlatformNotSupportedException("Workouts need a watch's health sensors.");
    }

    public Task EndWorkoutAsync()
    {
        throw new PlatformNotSupportedException("Workouts need a watch's health sensors.");
    }
}
