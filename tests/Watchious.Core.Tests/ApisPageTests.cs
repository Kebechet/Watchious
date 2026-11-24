using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Watchious.Essentials;
using Watchious.Hosting;
using Watchious.Samples.Counter;
using Xunit;

namespace Watchious.Core.Tests;

public class ApisPageTests
{
    // The page subscribes to nine events of services that outlive it; each subscription must go with the page.
    [Fact]
    public async Task ApisPage_WhenDisposed_NoLongerHandlesAnyWatchApiEvent()
    {
        // Arrange
        var apis = new EventRaisingApis();
        var builder = WatchiousApp.CreateBuilder();
        builder.Services.AddSingleton<IAppLifecycle>(apis);
        builder.Services.AddSingleton<IAmbientMode>(apis);
        builder.Services.AddSingleton<ISecureStorage>(apis);
        builder.Services.AddSingleton<ILocalNotifications>(apis);
        builder.Services.AddSingleton<IGeolocation>(apis);
        builder.Services.AddSingleton<IAccelerometer>(apis);
        builder.Services.AddSingleton<IGyroscope>(apis);
        builder.Services.AddSingleton<IMagnetometer>(apis);
        builder.Services.AddSingleton<ICompass>(apis);
        builder.Services.AddSingleton<IBarometer>(apis);
        builder.Services.AddSingleton<IOrientationSensor>(apis);
        var app = builder.UseWatchiousApp<ApisPage>().Build();
        var host = new RecordingHost();
        await app.StartAsync(host);
        var handledWhileShown = apis.RaiseEveryEvent();

        // Act
        await app.DisposeAsync();
        var handledAfterDispose = apis.RaiseEveryEvent();

        // Assert
        host.Errors.ShouldBeEmpty();
        handledWhileShown.ShouldBe(9);
        handledAfterDispose.ShouldBe(0);
    }

    // Real events, so that the page's own handlers are what is added and removed.
    private sealed class EventRaisingApis : IAppLifecycle, IAmbientMode, ISecureStorage, ILocalNotifications, IGeolocation,
        IAccelerometer, IGyroscope, IMagnetometer, ICompass, IBarometer, IOrientationSensor
    {
        private event EventHandler<AccelerometerChangedEventArgs>? AccelerometerChanged;

        private event EventHandler<GyroscopeChangedEventArgs>? GyroscopeChanged;

        private event EventHandler<MagnetometerChangedEventArgs>? MagnetometerChanged;

        private event EventHandler<CompassChangedEventArgs>? CompassChanged;

        private event EventHandler<BarometerChangedEventArgs>? BarometerChanged;

        private event EventHandler<OrientationSensorChangedEventArgs>? OrientationChanged;

        public event EventHandler<AppStateChangedEventArgs>? StateChanged;

        public event EventHandler<AmbientModeChangedEventArgs>? AmbientModeChanged;

        public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;

        public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;

        event EventHandler<AccelerometerChangedEventArgs>? IAccelerometer.ReadingChanged
        {
            add => AccelerometerChanged += value;
            remove => AccelerometerChanged -= value;
        }

        event EventHandler<GyroscopeChangedEventArgs>? IGyroscope.ReadingChanged
        {
            add => GyroscopeChanged += value;
            remove => GyroscopeChanged -= value;
        }

        event EventHandler<MagnetometerChangedEventArgs>? IMagnetometer.ReadingChanged
        {
            add => MagnetometerChanged += value;
            remove => MagnetometerChanged -= value;
        }

        event EventHandler<CompassChangedEventArgs>? ICompass.ReadingChanged
        {
            add => CompassChanged += value;
            remove => CompassChanged -= value;
        }

        event EventHandler<BarometerChangedEventArgs>? IBarometer.ReadingChanged
        {
            add => BarometerChanged += value;
            remove => BarometerChanged -= value;
        }

        event EventHandler<OrientationSensorChangedEventArgs>? IOrientationSensor.ReadingChanged
        {
            add => OrientationChanged += value;
            remove => OrientationChanged -= value;
        }

        public AppState State => AppState.Active;

        public bool IsSupported => true;

        public bool IsAmbient => false;

        public bool IsMonitoring => false;

        public bool IsListeningForeground => false;

        // Raises each event once and returns how many handlers ran.
        public int RaiseEveryEvent()
        {
            var location = new Location(49.19, 16.61, DateTimeOffset.UtcNow);
            return Raise(StateChanged, new AppStateChangedEventArgs(AppState.Background))
                + Raise(AmbientModeChanged, new AmbientModeChangedEventArgs(true))
                + Raise(LocationChanged, new GeolocationLocationChangedEventArgs(location))
                + Raise(ListeningFailed, new GeolocationListeningFailedEventArgs(GeolocationError.PositionUnavailable))
                + Raise(AccelerometerChanged, new AccelerometerChangedEventArgs(new AccelerometerData(Vector3.UnitZ)))
                + Raise(GyroscopeChanged, new GyroscopeChangedEventArgs(new GyroscopeData(Vector3.UnitX)))
                + Raise(MagnetometerChanged, new MagnetometerChangedEventArgs(new MagnetometerData(Vector3.UnitY)))
                + Raise(CompassChanged, new CompassChangedEventArgs(new CompassData(90)))
                + Raise(BarometerChanged, new BarometerChangedEventArgs(new BarometerData(1013)))
                + Raise(OrientationChanged, new OrientationSensorChangedEventArgs(new OrientationSensorData(Quaternion.Identity)));
        }

        public void Start(SensorSpeed sensorSpeed)
        {
        }

        public void Stop()
        {
        }

        public Task<string?> GetAsync(string key)
        {
            return Task.FromResult<string?>(null);
        }

        public Task SetAsync(string key, string value)
        {
            return Task.CompletedTask;
        }

        public bool Remove(string key)
        {
            return false;
        }

        public void RemoveAll()
        {
        }

        public Task<bool> RequestPermissionAsync()
        {
            return Task.FromResult(true);
        }

        public Task ShowAsync(LocalNotification notification)
        {
            return Task.CompletedTask;
        }

        public void Cancel(int id)
        {
        }

        public void CancelAll()
        {
        }

        public Task<Location?> GetLastKnownLocationAsync()
        {
            return Task.FromResult<Location?>(null);
        }

        public Task<Location?> GetLocationAsync(GeolocationRequest? request = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Location?>(null);
        }

        public Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
        {
            return Task.FromResult(true);
        }

        public void StopListeningForeground()
        {
        }

        private int Raise<TArgs>(EventHandler<TArgs>? handlers, TArgs args)
        {
            if (handlers is null)
            {
                return 0;
            }

            var count = handlers.GetInvocationList().Length;
            handlers(this, args);
            return count;
        }
    }
}
