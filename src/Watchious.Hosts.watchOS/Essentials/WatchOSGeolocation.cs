using System.Collections.Concurrent;
using Watchious.Essentials;

namespace Watchious.Hosts.watchOS.Essentials;

// CLLocationManager (platforms/watchOS/Host/Location.swift). Core Location has no minimum time between updates, so
// listening skips updates that come sooner than GeolocationListeningRequest.MinimumTime.
internal sealed class WatchOSGeolocation : IGeolocation, IDisposable
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Location?>> _requests = new();
    private readonly Lock _lock = new();
    private TimeSpan _minimumTime;
    private DateTimeOffset _lastDelivered = DateTimeOffset.MinValue;

    public WatchOSGeolocation()
    {
        WatchOSCallbacks.Geolocation = this;
    }

    public bool IsSupported => WatchOSNative.LocationIsSupported() == LocationSupport.Supported;

    public bool IsListeningForeground { get; private set; }

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;

    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;

    public async Task<Location?> GetLastKnownLocationAsync()
    {
        await EnsurePermissionAsync();
        return ReadLastKnown();
    }

    public async Task<Location?> GetLocationAsync(GeolocationRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GeolocationRequest();
        await EnsurePermissionAsync();
        var token = WatchOSCallbacks.NextToken();
        var result = new TaskCompletionSource<Location?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _requests[token] = result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        await using var registration = timeout.Token.Register(() =>
        {
            WatchOSNative.LocationCancel(token);
            if (_requests.TryRemove(token, out var pending))
            {
                pending.TrySetResult(null);
            }
        });
        WatchOSNative.LocationRequest(token, Accuracy(request.DesiredAccuracy));
        var location = await result.Task;
        cancellationToken.ThrowIfCancellationRequested();
        return location;
    }

    public async Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
    {
        await EnsurePermissionAsync();
        lock (_lock)
        {
            if (IsListeningForeground)
            {
                throw new InvalidOperationException("Already listening for location updates; call StopListeningForeground first.");
            }

            _minimumTime = request.MinimumTime;
            _lastDelivered = DateTimeOffset.MinValue;
            IsListeningForeground = true;
        }

        WatchOSNative.LocationStart(Accuracy(request.DesiredAccuracy));
        return true;
    }

    public void StopListeningForeground()
    {
        lock (_lock)
        {
            if (!IsListeningForeground)
            {
                return;
            }

            IsListeningForeground = false;
        }

        WatchOSNative.LocationStop();
    }

    public void Dispose()
    {
        StopListeningForeground();
        if (WatchOSCallbacks.Geolocation == this)
        {
            WatchOSCallbacks.Geolocation = null;
        }
    }

    // Token 0 is the listening; any other one a GetLocationAsync call.
    internal void OnNativeLocation(long token, Location location)
    {
        if (token != 0)
        {
            if (_requests.TryRemove(token, out var pending))
            {
                pending.TrySetResult(location);
            }

            return;
        }

        lock (_lock)
        {
            if (!IsListeningForeground || location.Timestamp - _lastDelivered < _minimumTime)
            {
                return;
            }

            _lastDelivered = location.Timestamp;
        }

        LocationChanged?.Invoke(this, new GeolocationLocationChangedEventArgs(location));
    }

    // Code 1 is denied, 2 no position.
    internal void OnNativeFailed(long token, int code)
    {
        if (token != 0)
        {
            if (_requests.TryRemove(token, out var pending))
            {
                pending.TrySetResult(null);
            }

            return;
        }

        var error = code == 1 ? GeolocationError.Unauthorized : GeolocationError.PositionUnavailable;
        ListeningFailed?.Invoke(this, new GeolocationListeningFailedEventArgs(error));
    }

    // Latitude, longitude, altitude, horizontal and vertical accuracy, speed, course, seconds since 1970. Core Location
    // marks an unknown accuracy, speed or course as negative, and an altitude as unknown by a negative vertical
    // accuracy.
    internal static Location ToLocation(ReadOnlySpan<double> values)
    {
        return new Location(values[0], values[1], DateTimeOffset.FromUnixTimeMilliseconds((long)(values[7] * 1000)))
        {
            Altitude = values[4] < 0 ? null : values[2],
            Accuracy = values[3] < 0 ? null : values[3],
            VerticalAccuracy = values[4] < 0 ? null : values[4],
            Speed = values[5] < 0 ? null : values[5],
            Course = values[6] < 0 ? null : values[6],
        };
    }

    private static unsafe Location? ReadLastKnown()
    {
        var values = stackalloc double[8];
        return WatchOSNative.LocationLastKnown(values) == 0 ? null : ToLocation(new ReadOnlySpan<double>(values, 8));
    }

    private static async Task EnsurePermissionAsync()
    {
        if (WatchOSNative.LocationIsSupported() == LocationSupport.LeftOut)
        {
            throw new FeatureNotSupportedException("Location is left out of this app: its Platforms/watchOS/Info.plist has no NSLocationWhenInUseUsageDescription, the text watchOS shows when it asks for location (and that App Store Connect requires of an app that uses location).");
        }

        var result = await WatchOSCallbacks.RunAsync(WatchOSNative.LocationRequestAuthorization);
        if (result == 2)
        {
            throw new PermissionException("The app's Platforms/watchOS/Info.plist needs NSLocationWhenInUseUsageDescription, the text watchOS shows when it asks for location.");
        }

        if (result != 1)
        {
            throw new PermissionException("The user didn't allow location.");
        }
    }

    // watchious_location_is_supported's answers.
    private static class LocationSupport
    {
        public const int Supported = 1;
        public const int LeftOut = 2;
    }

    // Core Location's desired accuracy in metres: kCLLocationAccuracyBest is -1.
    private static double Accuracy(GeolocationAccuracy accuracy)
    {
        return accuracy switch
        {
            GeolocationAccuracy.Lowest => 3000,
            GeolocationAccuracy.Low => 1000,
            GeolocationAccuracy.High => 10,
            GeolocationAccuracy.Best => -1,
            _ => 100,
        };
    }
}
