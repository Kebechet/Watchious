using Android;
using Android.App;
using Kebechet.Types.Health.Enums;
using Kebechet.Types.Health.Models;
using Watchious.Health.Native;

namespace Watchious.Health.WearOS;

// Wear OS Health Services through the module's Java API (platforms/WearOS/health).
internal sealed class WearOSHealthBackend : Java.Lang.Object, IHealthBackend, WatchiousHealthServices.IListener
{
    // Android 16 replaced BODY_SENSORS with this health permission.
    private const string ReadHeartRatePermission = "android.permission.health.READ_HEART_RATE";

    private readonly WatchiousHealthServices _healthServices = new(Application.Context);

    public bool IsSupported => true;

    public HealthDataSdk DataSdk => HealthDataSdk.GoogleHealthServices;

    public string? DataOrigin => Application.Context.PackageName;

    public Action<double, DateTimeOffset>? HeartRateReceived { get; set; }

    public Action? WorkoutEnded { get; set; }

    public Task<IList<string>> RequestPermissionsAsync(IList<HealthPermissionDto> permissions, CancellationToken cancellationToken)
    {
        var androidPermissions = permissions.SelectMany(ToAndroidPermissions).Distinct().ToArray();
        var result = new TaskCompletionSource<IList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellation = cancellationToken.Register(() => result.TrySetCanceled(cancellationToken));
        _ = result.Task.ContinueWith(_ => cancellation.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        WatchiousPermissionActivity.Request(Application.Context, androidPermissions, new PermissionCallback(result));
        return result.Task;
    }

    public Task StartWorkoutAsync(ActivityType activityType)
    {
        return Run(callback => _healthServices.StartExercise(activityType.ToString(), this, callback));
    }

    public Task PauseWorkoutAsync()
    {
        return Run(_healthServices.PauseExercise);
    }

    public Task ResumeWorkoutAsync()
    {
        return Run(_healthServices.ResumeExercise);
    }

    public Task EndWorkoutAsync()
    {
        return Run(_healthServices.EndExercise);
    }

    public void OnHeartRate(double beatsPerMinute, long epochMilliseconds)
    {
        HeartRateReceived?.Invoke(beatsPerMinute, DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds));
    }

    public void OnEnded()
    {
        WorkoutEnded?.Invoke();
    }

    private static IEnumerable<string> ToAndroidPermissions(HealthPermissionDto permission)
    {
        var heartRate = OperatingSystem.IsAndroidVersionAtLeast(36) ? ReadHeartRatePermission : Manifest.Permission.BodySensors;
        return permission.HealthDataType switch
        {
            HealthDataType.HeartRate => [heartRate],
            HealthDataType.ExerciseSession => [Manifest.Permission.ActivityRecognition, heartRate],
            _ => [],
        };
    }

    private static Task Run(Action<WatchiousHealthServices.ICallback> start)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        start(new CompletionCallback(result));
        return result.Task;
    }

    private sealed class CompletionCallback(TaskCompletionSource result) : Java.Lang.Object, WatchiousHealthServices.ICallback
    {
        public void OnComplete(string? error)
        {
            if (error is null)
            {
                result.TrySetResult();
                return;
            }

            result.TrySetException(new InvalidOperationException(error));
        }
    }

    private sealed class PermissionCallback(TaskCompletionSource<IList<string>> result)
        : Java.Lang.Object, WatchiousPermissionActivity.ICallback
    {
        public void OnResult(string[] deniedPermissions)
        {
            result.TrySetResult(deniedPermissions);
        }
    }
}
