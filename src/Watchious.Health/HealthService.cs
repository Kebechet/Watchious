using Kebechet.Types.Health.Constants;
using Kebechet.Types.Health.Enums;
using Kebechet.Types.Health.Enums.Errors;
using Kebechet.Types.Health.Extensions;
using Kebechet.Types.Health.Models;
using Kebechet.Types.Health.Models.Metrics;

namespace Watchious.Health;

// What each platform implements under HealthService.
internal interface IHealthBackend
{
    bool IsSupported { get; }

    HealthDataSdk DataSdk { get; }

    // The app's own identifier (package name / bundle identifier), which Kebechet.Maui.Health puts in DataOrigin.
    string? DataOrigin { get; }

    // The denied permissions' names.
    Task<IList<string>> RequestPermissionsAsync(IList<HealthPermissionDto> permissions, CancellationToken cancellationToken);

    Task StartWorkoutAsync(ActivityType activityType);

    Task PauseWorkoutAsync();

    Task ResumeWorkoutAsync();

    Task EndWorkoutAsync();

    // Set by HealthService before a workout starts.
    Action<double, DateTimeOffset>? HeartRateReceived { get; set; }
}

internal sealed class HealthService(IHealthBackend backend) : IHealthService, IHealthWorkoutService, IDisposable
{
    private readonly Lock _lock = new();
    private WorkoutSession? _session;
    private int _heartRateCount;
    private double _heartRateSum;
    private double? _maxHeartRate;
    private double? _minHeartRate;

    public event EventHandler<HeartRateDto>? HeartRateReceived;

    public bool IsSupported => backend.IsSupported;

    public IHealthWorkoutService Activity => this;

    public async Task<RequestPermissionResult> RequestPermissions(
        IList<HealthPermissionDto> healthPermissions,
        CancellationToken cancellationToken = default)
    {
        if (!backend.IsSupported)
        {
            return new RequestPermissionResult { Error = RequestPermissionError.SdkUnavailable };
        }

        try
        {
            var denied = await backend.RequestPermissionsAsync(healthPermissions, cancellationToken);
            return new RequestPermissionResult { DeniedPermissions = denied };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new RequestPermissionResult { Error = RequestPermissionError.ProblemWhileGrantingPermissions, ErrorException = exception };
        }
    }

    public Task<WorkoutSession?> GetActive()
    {
        lock (_lock)
        {
            return Task.FromResult(_session);
        }
    }

    public Task<bool> IsRunning()
    {
        lock (_lock)
        {
            return Task.FromResult(_session?.State == WorkoutSessionState.Running);
        }
    }

    public Task<bool> IsPaused()
    {
        lock (_lock)
        {
            return Task.FromResult(_session?.State == WorkoutSessionState.Paused);
        }
    }

    public async Task Start(ActivityType activityType, string? title = null)
    {
        lock (_lock)
        {
            if (_session is not null)
            {
                throw new InvalidOperationException("A workout is already running; end it first.");
            }

            _session = new WorkoutSession(Guid.NewGuid().ToString(), activityType, title, backend.DataOrigin, DateTimeOffset.UtcNow);
            _heartRateCount = 0;
            _heartRateSum = 0;
            _maxHeartRate = null;
            _minHeartRate = null;
        }

        backend.HeartRateReceived = OnHeartRate;
        try
        {
            await backend.StartWorkoutAsync(activityType);
        }
        catch
        {
            backend.HeartRateReceived = null;
            lock (_lock)
            {
                _session = null;
            }

            throw;
        }
    }

    public async Task Pause()
    {
        var session = RequireSession(WorkoutSessionState.Running);
        await backend.PauseWorkoutAsync();
        lock (_lock)
        {
            session.Pause();
        }
    }

    public async Task Resume()
    {
        var session = RequireSession(WorkoutSessionState.Paused);
        await backend.ResumeWorkoutAsync();
        lock (_lock)
        {
            session.Resume();
        }
    }

    public async Task<WorkoutDto?> End()
    {
        WorkoutSession? session;
        lock (_lock)
        {
            session = _session;
        }

        if (session is null)
        {
            return null;
        }

        await backend.EndWorkoutAsync();
        backend.HeartRateReceived = null;
        lock (_lock)
        {
            var endTime = DateTimeOffset.UtcNow;
            session.End();
            _session = null;
            return session.ToWorkoutDto(
                backend.DataSdk,
                endTime,
                averageHeartRate: _heartRateCount == 0 ? null : _heartRateSum / _heartRateCount,
                maxHeartRate: _maxHeartRate,
                minHeartRate: _minHeartRate);
        }
    }

    public void Dispose()
    {
        backend.HeartRateReceived = null;
    }

    private void OnHeartRate(double beatsPerMinute, DateTimeOffset timestamp)
    {
        lock (_lock)
        {
            if (_session is null)
            {
                return;
            }

            _heartRateCount++;
            _heartRateSum += beatsPerMinute;
            _maxHeartRate = Math.Max(_maxHeartRate ?? beatsPerMinute, beatsPerMinute);
            _minHeartRate = Math.Min(_minHeartRate ?? beatsPerMinute, beatsPerMinute);
        }

        HeartRateReceived?.Invoke(this, new HeartRateDto
        {
            Id = Guid.NewGuid().ToString(),
            DataSdk = backend.DataSdk,
            DataOrigin = backend.DataOrigin,
            Timestamp = timestamp,
            BeatsPerMinute = beatsPerMinute,
            Unit = Units.BeatsPerMinute,
        });
    }

    private WorkoutSession RequireSession(WorkoutSessionState state)
    {
        lock (_lock)
        {
            if (_session is null || _session.State != state)
            {
                throw new InvalidOperationException($"No workout is {state.ToString().ToLowerInvariant()}.");
            }

            return _session;
        }
    }
}
