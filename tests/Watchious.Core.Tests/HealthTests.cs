using Kebechet.Types.Health.Constants;
using Kebechet.Types.Health.Enums;
using Kebechet.Types.Health.Enums.Errors;
using Kebechet.Types.Health.Models;
using Kebechet.Types.Health.Models.Metrics;
using Shouldly;
using Watchious.Health;
using Xunit;

namespace Watchious.Core.Tests;

public class HealthTests
{
    [Fact]
    public async Task Workout_GoesThroughRunningPausedAndEnded()
    {
        // Arrange
        var backend = new FakeBackend();
        var health = new HealthService(backend);
        var states = new List<WorkoutSessionState?>();

        // Act
        await health.Activity.Start(ActivityType.Running, "Morning run");
        states.Add((await health.Activity.GetActive())?.State);
        await health.Activity.Pause();
        states.Add((await health.Activity.GetActive())?.State);
        var isPaused = await health.Activity.IsPaused();
        await health.Activity.Resume();
        var isRunning = await health.Activity.IsRunning();
        var workout = await health.Activity.End();

        // Assert
        states.ShouldBe([WorkoutSessionState.Running, WorkoutSessionState.Paused]);
        isPaused.ShouldBeTrue();
        isRunning.ShouldBeTrue();
        backend.Calls.ShouldBe(["start Running", "pause", "resume", "end"]);
        workout!.ActivityType.ShouldBe(ActivityType.Running);
        workout.Title.ShouldBe("Morning run");
        workout.DataSdk.ShouldBe(HealthDataSdk.GoogleHealthServices);
        workout.DataOrigin.ShouldBe("com.kebechet.watch");
        workout.Metadata![WorkoutMetadata.PauseCount].ShouldBe(1);
        (await health.Activity.GetActive()).ShouldBeNull();
    }

    [Fact]
    public async Task HeartRateReceived_DuringAWorkout_CarriesTheUnitAndOriginOfMauiHealth()
    {
        // Arrange
        var backend = new FakeBackend();
        var health = new HealthService(backend);
        var received = new List<HeartRateDto>();
        void OnHeartRate(object? sender, HeartRateDto heartRate) => received.Add(heartRate);
        health.Activity.HeartRateReceived += OnHeartRate;
        await health.Activity.Start(ActivityType.Running);

        // Act
        backend.SendHeartRate(128);
        health.Activity.HeartRateReceived -= OnHeartRate;

        // Assert
        received.ShouldHaveSingleItem();
        received[0].Unit.ShouldBe(Units.BeatsPerMinute);
        received[0].DataOrigin.ShouldBe("com.kebechet.watch");
        received[0].DataSdk.ShouldBe(HealthDataSdk.GoogleHealthServices);
    }

    [Fact]
    public async Task End_SummarizesTheHeartRateOfTheWorkout()
    {
        // Arrange
        var backend = new FakeBackend();
        var health = new HealthService(backend);
        var received = new List<double>();
        void OnHeartRate(object? sender, HeartRateDto heartRate) => received.Add(heartRate.BeatsPerMinute);
        health.Activity.HeartRateReceived += OnHeartRate;
        await health.Activity.Start(ActivityType.Cycling);

        // Act
        backend.SendHeartRate(120);
        backend.SendHeartRate(150);
        backend.SendHeartRate(135);
        var workout = await health.Activity.End();
        backend.SendHeartRate(90);
        health.Activity.HeartRateReceived -= OnHeartRate;

        // Assert
        received.ShouldBe([120, 150, 135]);
        workout!.AverageHeartRate.ShouldBe(135);
        workout.MaxHeartRate.ShouldBe(150);
        workout.MinHeartRate.ShouldBe(120);
        backend.HeartRateReceived.ShouldBeNull();
    }

    [Fact]
    public async Task Start_WhileAWorkoutRuns_Throws()
    {
        // Arrange
        var health = new HealthService(new FakeBackend());
        await health.Activity.Start(ActivityType.Walking);

        // Act
        var startAgain = () => health.Activity.Start(ActivityType.Running);

        // Assert
        await startAgain.ShouldThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Start_TheWatchRefuses_LeavesNoWorkout()
    {
        // Arrange
        var health = new HealthService(new FakeBackend { StartError = new InvalidOperationException("No sensors") });

        // Act
        var start = () => health.Activity.Start(ActivityType.Walking);

        // Assert
        await start.ShouldThrowAsync<InvalidOperationException>();
        (await health.Activity.GetActive()).ShouldBeNull();
    }

    [Fact]
    public async Task End_WithoutAWorkout_ReturnsNull()
    {
        // Arrange
        var health = new HealthService(new FakeBackend());

        // Act
        var workout = await health.Activity.End();

        // Assert
        workout.ShouldBeNull();
    }

    [Fact]
    public async Task RequestPermissions_ReportsTheDeniedOnes()
    {
        // Arrange
        var health = new HealthService(new FakeBackend { Denied = ["android.permission.BODY_SENSORS"] });

        // Act
        var result = await health.RequestPermissions([new HealthPermissionDto { HealthDataType = HealthDataType.HeartRate, PermissionType = PermissionType.Read }], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.DeniedPermissions.ShouldBe(["android.permission.BODY_SENSORS"]);
    }

    [Fact]
    public async Task RequestPermissions_WithoutHealthSensors_ReportsTheSdkUnavailable()
    {
        // Arrange
        var health = new HealthService(new UnavailableHealthBackend());

        // Act
        var result = await health.RequestPermissions([new HealthPermissionDto { HealthDataType = HealthDataType.HeartRate, PermissionType = PermissionType.Read }], TestContext.Current.CancellationToken);

        // Assert
        health.IsSupported.ShouldBeFalse();
        result.Error.ShouldBe(RequestPermissionError.SdkUnavailable);
    }

    [Fact]
    public async Task Dispose_ClearsTheHeartRateCallback()
    {
        // Arrange
        var backend = new FakeBackend();
        var health = new HealthService(backend);
        await health.Activity.Start(ActivityType.Yoga);

        // Act
        health.Dispose();

        // Assert
        backend.HeartRateReceived.ShouldBeNull();
    }

    [Fact]
    public async Task Workout_EndedByThePlatform_EndsTheSession()
    {
        // Arrange
        var backend = new FakeBackend();
        var health = new HealthService(backend);
        await health.Activity.Start(ActivityType.Running);

        // Act
        backend.EndFromThePlatform();

        // Assert
        var isRunning = await health.Activity.IsRunning();
        isRunning.ShouldBeFalse();
        var workout = await health.Activity.End();
        workout.ShouldBeNull();
        await health.Activity.Start(ActivityType.Walking);
        backend.Calls.ShouldBe(["start Running", "start Walking"]);
    }

    [Fact]
    public async Task Dispose_EndsARunningWorkout()
    {
        // Arrange
        var backend = new FakeBackend();
        var health = new HealthService(backend);
        await health.Activity.Start(ActivityType.Yoga);

        // Act
        health.Dispose();

        // Assert
        backend.Calls.ShouldBe(["start Yoga", "end"]);
        backend.WorkoutEnded.ShouldBeNull();
    }

    private sealed class FakeBackend : IHealthBackend
    {
        public List<string> Calls { get; } = [];

        public IList<string> Denied { get; init; } = [];

        public Exception? StartError { get; init; }

        public bool IsSupported => true;

        public HealthDataSdk DataSdk => HealthDataSdk.GoogleHealthServices;

        public string? DataOrigin => "com.kebechet.watch";

        public Action<double, DateTimeOffset>? HeartRateReceived { get; set; }

        public Action? WorkoutEnded { get; set; }

        public void EndFromThePlatform()
        {
            WorkoutEnded?.Invoke();
        }

        public void SendHeartRate(double beatsPerMinute)
        {
            HeartRateReceived?.Invoke(beatsPerMinute, DateTimeOffset.UtcNow);
        }

        public Task<IList<string>> RequestPermissionsAsync(IList<HealthPermissionDto> permissions, CancellationToken cancellationToken)
        {
            return Task.FromResult(Denied);
        }

        public Task StartWorkoutAsync(ActivityType activityType)
        {
            Calls.Add($"start {activityType}");
            return StartError is null ? Task.CompletedTask : Task.FromException(StartError);
        }

        public Task PauseWorkoutAsync()
        {
            Calls.Add("pause");
            return Task.CompletedTask;
        }

        public Task ResumeWorkoutAsync()
        {
            Calls.Add("resume");
            return Task.CompletedTask;
        }

        public Task EndWorkoutAsync()
        {
            Calls.Add("end");
            return Task.CompletedTask;
        }
    }
}
