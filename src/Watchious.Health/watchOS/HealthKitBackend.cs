using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Kebechet.Types.Health.Enums;
using Kebechet.Types.Health.Models;

namespace Watchious.Health;

// HealthKit workouts through the SwiftUI host (watchOS/HealthKitWorkout.swift, which Watchious.Sdk compiles into the
// host): HKWorkoutSession with an HKLiveWorkoutBuilder, whose heart rate statistics come back through a callback.
// Asynchronous HealthKit calls complete through another callback, matched by a token.
internal sealed partial class HealthKitBackend : IHealthBackend, IDisposable
{
    // The registered instance, which the native callbacks (static by nature) reach. The service is a singleton.
    private static HealthKitBackend? _current;

    // HKObjectType.workoutType()'s identifier.
    private const string WorkoutTypeIdentifier = "HKWorkoutTypeIdentifier";

    private readonly ConcurrentDictionary<long, TaskCompletionSource> _pending = new();
    private long _lastToken;

    public unsafe HealthKitBackend()
    {
        _current = this;
        SetCallbacks(&OnHeartRate, &OnCompleted, &OnWorkoutEnded);
    }

    public bool IsSupported => IsHealthDataAvailable() != 0;

    public HealthDataSdk DataSdk => HealthDataSdk.AppleHealthKit;

    public string? DataOrigin { get; } = BundleIdentifier();

    public Action<double, DateTimeOffset>? HeartRateReceived { get; set; }

    public Action? WorkoutEnded { get; set; }

    // HealthKit never tells an app whether it may read: an app learns only that the user answered. It does tell
    // whether the app may save workouts, which starting one needs, so that is the permission reported denied.
    // ExerciseSession always asks to save workouts, whatever its PermissionType, because a workout session needs it.
    public async Task<IList<string>> RequestPermissionsAsync(IList<HealthPermissionDto> permissions, CancellationToken cancellationToken)
    {
        var readHeartRate = permissions.Any(permission => permission.HealthDataType is HealthDataType.HeartRate or HealthDataType.ExerciseSession);
        var writeWorkouts = permissions.Any(permission => permission.HealthDataType == HealthDataType.ExerciseSession);
        await RunAsync(token => RequestAuthorization(readHeartRate ? 1 : 0, writeWorkouts ? 1 : 0, token)).WaitAsync(cancellationToken);
        var isWorkoutSharingAuthorized = IsWorkoutSharingAuthorized() != 0;
        return writeWorkouts && !isWorkoutSharingAuthorized ? [WorkoutTypeIdentifier] : [];
    }

    public Task StartWorkoutAsync(ActivityType activityType)
    {
        return RunAsync(token => StartWorkout(activityType.ToString(), token));
    }

    public Task PauseWorkoutAsync()
    {
        PauseWorkout();
        return Task.CompletedTask;
    }

    public Task ResumeWorkoutAsync()
    {
        ResumeWorkout();
        return Task.CompletedTask;
    }

    public Task EndWorkoutAsync()
    {
        return RunAsync(EndWorkout);
    }

    public async Task<bool> RecoverWorkoutAsync()
    {
        await RunAsync(RecoverWorkout);
        return HasWorkout() != 0;
    }

    // The session runs on in HealthKit, which keeps the app running in the background; its updates reach no one until
    // the app picks the workout up again.
    public void Detach()
    {
    }

    public void Dispose()
    {
        Interlocked.CompareExchange(ref _current, null, this);
    }

    private Task RunAsync(Action<long> start)
    {
        var token = Interlocked.Increment(ref _lastToken);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[token] = completion;
        start(token);
        return completion.Task;
    }

    // The native callbacks must not let exceptions escape into Swift.
    [UnmanagedCallersOnly]
    private static void OnHeartRate(double beatsPerMinute, double unixSeconds)
    {
        try
        {
            _current?.HeartRateReceived?.Invoke(beatsPerMinute, DateTimeOffset.FromUnixTimeMilliseconds((long)(unixSeconds * 1000)));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious.Health: {exception}");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWorkoutEnded()
    {
        try
        {
            _current?.WorkoutEnded?.Invoke();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious.Health: {exception}");
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void OnCompleted(long token, byte* error)
    {
        try
        {
            var current = _current;
            if (current is null || !current._pending.TryRemove(token, out var completion))
            {
                return;
            }

            if (error is null)
            {
                completion.TrySetResult();
                return;
            }

            completion.TrySetException(new InvalidOperationException(Marshal.PtrToStringUTF8((nint)error)));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious.Health: {exception}");
        }
    }

    [LibraryImport("__Internal", EntryPoint = "watchious_health_set_callbacks")]
    private static unsafe partial void SetCallbacks(
        delegate* unmanaged<double, double, void> onHeartRate,
        delegate* unmanaged<long, byte*, void> onCompleted,
        delegate* unmanaged<void> onWorkoutEnded);

    [LibraryImport("__Internal", EntryPoint = "watchious_health_is_available")]
    private static partial int IsHealthDataAvailable();

    [LibraryImport("__Internal", EntryPoint = "watchious_health_workout_sharing_authorized")]
    private static partial int IsWorkoutSharingAuthorized();

    [LibraryImport("__Internal", EntryPoint = "watchious_health_request_authorization")]
    private static partial void RequestAuthorization(int readHeartRate, int writeWorkouts, long token);

    // The marshaller frees the strdup'd copy: Marshal.FreeCoTaskMem is free() on Apple platforms.
    [LibraryImport("__Internal", EntryPoint = "watchious_health_bundle_identifier", StringMarshalling = StringMarshalling.Utf8)]
    private static partial string? BundleIdentifier();

    // activityType is Kebechet.Types.Health.Enums.ActivityType's name.
    [LibraryImport("__Internal", EntryPoint = "watchious_health_start_workout", StringMarshalling = StringMarshalling.Utf8)]
    private static partial void StartWorkout(string activityType, long token);

    [LibraryImport("__Internal", EntryPoint = "watchious_health_pause_workout")]
    private static partial void PauseWorkout();

    [LibraryImport("__Internal", EntryPoint = "watchious_health_resume_workout")]
    private static partial void ResumeWorkout();

    [LibraryImport("__Internal", EntryPoint = "watchious_health_recover_workout")]
    private static partial void RecoverWorkout(long token);

    [LibraryImport("__Internal", EntryPoint = "watchious_health_has_workout")]
    private static partial int HasWorkout();

    [LibraryImport("__Internal", EntryPoint = "watchious_health_end_workout")]
    private static partial void EndWorkout(long token);
}
