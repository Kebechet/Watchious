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
    public async Task ApisPage_WhenDisposed_UnsubscribesFromEveryWatchApi()
    {
        // Arrange
        var apis = new SubscriptionCountingApis();
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
        var subscribedWhileShown = apis.Subscriptions;

        // Act
        await app.DisposeAsync();

        // Assert
        host.Errors.ShouldBeEmpty();
        subscribedWhileShown.ShouldBe(9);
        apis.Subscriptions.ShouldBe(0);
    }

    private sealed class SubscriptionCountingApis : IAppLifecycle, IAmbientMode, ISecureStorage, ILocalNotifications, IGeolocation,
        IAccelerometer, IGyroscope, IMagnetometer, ICompass, IBarometer, IOrientationSensor
    {
        private int _subscriptions;

        public int Subscriptions => Volatile.Read(ref _subscriptions);

        public AppState State => AppState.Active;

        public bool IsSupported => true;

        public bool IsAmbient => false;

        public bool IsMonitoring => false;

        public bool IsListeningForeground => false;

        event EventHandler<AppStateChangedEventArgs>? IAppLifecycle.StateChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<AmbientModeChangedEventArgs>? IAmbientMode.AmbientModeChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<GeolocationLocationChangedEventArgs>? IGeolocation.LocationChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<GeolocationListeningFailedEventArgs>? IGeolocation.ListeningFailed
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<AccelerometerChangedEventArgs>? IAccelerometer.ReadingChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<GyroscopeChangedEventArgs>? IGyroscope.ReadingChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<MagnetometerChangedEventArgs>? IMagnetometer.ReadingChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<CompassChangedEventArgs>? ICompass.ReadingChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<BarometerChangedEventArgs>? IBarometer.ReadingChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
        }

        event EventHandler<OrientationSensorChangedEventArgs>? IOrientationSensor.ReadingChanged
        {
            add => Subscribe();
            remove => Unsubscribe();
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

        private void Subscribe()
        {
            Interlocked.Increment(ref _subscriptions);
        }

        private void Unsubscribe()
        {
            Interlocked.Decrement(ref _subscriptions);
        }
    }
}
