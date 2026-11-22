using Kebechet.Types.Health.Enums;
using Kebechet.Types.Health.Models;
using Kebechet.Types.Health.Models.Metrics;

namespace Watchious.Health;

// Kebechet.Maui.Health's service shape and types (Kebechet.Types.Health), for what a watch does: permissions and live
// workouts. HealthKit on watchOS, Health Services on Wear OS.
public interface IHealthService
{
    bool IsSupported { get; }

    IHealthWorkoutService Activity { get; }

    Task<RequestPermissionResult> RequestPermissions(
        IList<HealthPermissionDto> healthPermissions,
        CancellationToken cancellationToken = default);
}

public interface IHealthWorkoutService
{
    Task<WorkoutSession?> GetActive();

    Task<bool> IsRunning();

    Task<bool> IsPaused();

    Task Start(ActivityType activityType, string? title = null);

    Task Pause();

    Task Resume();

    // The finished workout, with the heart rate statistics the watch measured.
    Task<WorkoutDto?> End();

    // The watch's own: heart rate while a workout runs, about every second or few. Raised on a background thread.
    // Subscribers unsubscribe when they are disposed.
    event EventHandler<HeartRateDto>? HeartRateReceived;
}
