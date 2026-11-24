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

    // After the app was closed or restarted: reattaches to a workout of this app that the platform still runs, and
    // tells whether there is one. HeartRateReceived and WorkoutEnded are set before.
    Task<bool> RecoverWorkoutAsync();

    // The app goes away while the workout runs on: no more updates, and the workout isn't ended.
    void Detach();

    // Set by HealthService before a workout starts.
    Action<double, DateTimeOffset>? HeartRateReceived { get; set; }

    // Set by HealthService before a workout starts. The workout ended: through EndWorkoutAsync, or on its own when
    // another app's workout took over or the session failed.
    Action? WorkoutEnded { get; set; }
}

// A workout outlives the app: the platform keeps it running (HealthKit's session keeps the app running in the
// background; on Wear OS a foreground service does, with an ongoing activity that brings the user back), and the next
// HealthService picks it up again from what the store kept (https://github.com/Kebechet/Watchious/issues/13).
internal sealed class HealthService : IHealthService, IHealthWorkoutService, IDisposable
{
    private readonly IHealthBackend _backend;
    private readonly IWorkoutStore? _store;
    private readonly Task _recovery;
    private readonly Lock _lock = new();
    private WorkoutSession? _session;
    private bool _isEnding;
    private bool _isDisposed;
    private int _heartRateCount;
    private double _heartRateSum;
    private double? _maxHeartRate;
    private double? _minHeartRate;

    public HealthService(IHealthBackend backend, IWorkoutStore? store = null)
    {
        _backend = backend;
        _store = store;
        _recovery = store is null ? Task.CompletedTask : RecoverAsync(store);
    }

    public event EventHandler<HeartRateDto>? HeartRateReceived;

    public bool IsSupported => _backend.IsSupported;

    public IHealthWorkoutService Activity => this;

    public async Task<RequestPermissionResult> RequestPermissions(
        IList<HealthPermissionDto> healthPermissions,
        CancellationToken cancellationToken = default)
    {
        if (!_backend.IsSupported)
        {
            return new RequestPermissionResult { Error = RequestPermissionError.SdkUnavailable };
        }

        try
        {
            var denied = await _backend.RequestPermissionsAsync(healthPermissions, cancellationToken);
            return new RequestPermissionResult { DeniedPermissions = denied };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new RequestPermissionResult { Error = RequestPermissionError.ProblemWhileGrantingPermissions, ErrorException = exception };
        }
    }

    public async Task<WorkoutSession?> GetActive()
    {
        await _recovery;
        lock (_lock)
        {
            return _session;
        }
    }

    public async Task<bool> IsRunning()
    {
        await _recovery;
        lock (_lock)
        {
            return _session?.State == WorkoutSessionState.Running;
        }
    }

    public async Task<bool> IsPaused()
    {
        await _recovery;
        lock (_lock)
        {
            return _session?.State == WorkoutSessionState.Paused;
        }
    }

    public async Task Start(ActivityType activityType, string? title = null)
    {
        await _recovery;
        lock (_lock)
        {
            if (_session is not null)
            {
                throw new InvalidOperationException("A workout is already running; end it first.");
            }

            _session = new WorkoutSession(Guid.NewGuid().ToString(), activityType, title, _backend.DataOrigin, DateTimeOffset.UtcNow);
            _heartRateCount = 0;
            _heartRateSum = 0;
            _maxHeartRate = null;
            _minHeartRate = null;
        }

        _backend.HeartRateReceived = OnHeartRate;
        _backend.WorkoutEnded = OnWorkoutEnded;
        try
        {
            await _backend.StartWorkoutAsync(activityType);
        }
        catch
        {
            _backend.HeartRateReceived = null;
            _backend.WorkoutEnded = null;
            lock (_lock)
            {
                _session = null;
            }

            throw;
        }

        Save();
    }

    public async Task Pause()
    {
        await _recovery;
        var session = RequireSession(WorkoutSessionState.Running);
        await _backend.PauseWorkoutAsync();
        lock (_lock)
        {
            session.Pause();
        }

        Save();
    }

    public async Task Resume()
    {
        await _recovery;
        var session = RequireSession(WorkoutSessionState.Paused);
        await _backend.ResumeWorkoutAsync();
        lock (_lock)
        {
            session.Resume();
        }

        Save();
    }

    public async Task<WorkoutDto?> End()
    {
        await _recovery;
        WorkoutSession? session;
        lock (_lock)
        {
            session = _session;
            if (session is null)
            {
                return null;
            }

            _isEnding = true;
        }

        try
        {
            await _backend.EndWorkoutAsync();
        }
        finally
        {
            lock (_lock)
            {
                _isEnding = false;
            }
        }

        _backend.HeartRateReceived = null;
        _backend.WorkoutEnded = null;
        _store?.Clear();
        lock (_lock)
        {
            var endTime = DateTimeOffset.UtcNow;
            session.End();
            _session = null;
            return session.ToWorkoutDto(
                _backend.DataSdk,
                endTime,
                averageHeartRate: _heartRateCount == 0 ? null : _heartRateSum / _heartRateCount,
                maxHeartRate: _maxHeartRate,
                minHeartRate: _minHeartRate);
        }
    }

    // A running workout runs on, and the next HealthService picks it up again. Without a store (the desktop preview)
    // nothing could, so there it ends with the app.
    public void Dispose()
    {
        bool endWorkout;
        lock (_lock)
        {
            _isDisposed = true;
            _backend.HeartRateReceived = null;
            _backend.WorkoutEnded = null;
            endWorkout = _store is null && _session is not null && !_isEnding;
        }

        Save();
        lock (_lock)
        {
            _session = null;
        }

        _ = LeaveAsync(endWorkout);
    }

    private async Task LeaveAsync(bool endWorkout)
    {
        try
        {
            if (endWorkout)
            {
                await _backend.EndWorkoutAsync();
            }
            else
            {
                _backend.Detach();
            }
        }
        catch (Exception)
        {
            // The app is going away: no one is left to report it to.
        }
        finally
        {
            (_backend as IDisposable)?.Dispose();
        }
    }

    private async Task RecoverAsync(IWorkoutStore store)
    {
        var saved = store.Load();
        if (saved is null)
        {
            return;
        }

        _backend.HeartRateReceived = OnHeartRate;
        _backend.WorkoutEnded = OnWorkoutEnded;
        bool isRunning;
        try
        {
            isRunning = await _backend.RecoverWorkoutAsync();
        }
        catch (Exception)
        {
            isRunning = false;
        }

        lock (_lock)
        {
            if (isRunning && !_isDisposed)
            {
                List<DateRange> pauses = [.. saved.Pauses.Select(pause => new DateRange(pause.Start, pause.End))];
                _session = new WorkoutSession(saved.Id, saved.ActivityType, saved.Title, _backend.DataOrigin, saved.StartTime, saved.State, pauses);
                _heartRateCount = saved.HeartRateCount;
                _heartRateSum = saved.HeartRateSum;
                _maxHeartRate = saved.MaxHeartRate;
                _minHeartRate = saved.MinHeartRate;
                return;
            }

            _backend.HeartRateReceived = null;
            _backend.WorkoutEnded = null;
        }

        // The workout ended while the app was away. (When this HealthService was disposed first, the workout runs on
        // for the next one.)
        if (!isRunning)
        {
            store.Clear();
        }
    }

    // What the store keeps between app runs.
    private void Save()
    {
        if (_store is null)
        {
            return;
        }

        SavedWorkout? saved;
        lock (_lock)
        {
            var session = _session;
            saved = session is null
                ? null
                : new SavedWorkout(
                    session.Id,
                    session.ActivityType,
                    session.Title,
                    session.StartTime,
                    session.State,
                    [.. session.PauseIntervals.Select(pause => new SavedPause(pause.Start, pause.End))],
                    _heartRateCount,
                    _heartRateSum,
                    _maxHeartRate,
                    _minHeartRate);
        }

        if (saved is null)
        {
            return;
        }

        try
        {
            _store.Save(saved);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Then a restarted app can't pick the workout up again; this one still ends it normally.
        }
    }

    // The platform ended the workout on its own. The session is over, though without a WorkoutDto to return.
    private void OnWorkoutEnded()
    {
        lock (_lock)
        {
            if (_session is null || _isEnding)
            {
                return;
            }

            _session.End();
            _session = null;
            _backend.HeartRateReceived = null;
            _backend.WorkoutEnded = null;
        }

        _store?.Clear();
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
            DataSdk = _backend.DataSdk,
            DataOrigin = _backend.DataOrigin,
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
