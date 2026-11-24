using Android;
using Android.Content;
using Android.Locations;
using Android.OS;
using Watchious.Essentials;
using AndroidLocation = Android.Locations.Location;
using Location = Watchious.Essentials.Location;

namespace Watchious.Hosts.WearOS.Essentials;

// Android's LocationManager. The fused provider (which may use the phone's position) serves most requests, at the
// requested accuracy from Android 12; High and Best prefer the watch's own GPS when it has one. Balanced accuracy
// (Default, Medium) doesn't turn GPS on, so away from its phone and Wi-Fi a watch may find no position for it.
internal sealed class WearOSGeolocation(Context context, WearOSPermissions permissions) : IGeolocation, IDisposable
{
    private static readonly string[] LocationPermissions = [Manifest.Permission.AccessFineLocation, Manifest.Permission.AccessCoarseLocation];

    private readonly LocationManager _manager = (LocationManager)context.GetSystemService(Context.LocationService)!;
    private readonly Lock _lock = new();
    private Listener? _listener;

    public bool IsSupported => _manager.AllProviders.Count > 0;

    public bool IsListeningForeground => _listener is not null;

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;

    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;

    public async Task<Location?> GetLastKnownLocationAsync()
    {
        await EnsurePermissionAsync();
        var latest = _manager.GetProviders(true)
            .Select(_manager.GetLastKnownLocation)
            .Where(location => location is not null)
            .MaxBy(location => location!.Time);
        return latest is null ? null : ToLocation(latest);
    }

    public async Task<Location?> GetLocationAsync(GeolocationRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= new GeolocationRequest();
        await EnsurePermissionAsync();
        var provider = ChooseProvider(request.DesiredAccuracy);
        if (provider is null)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        using var signal = new CancellationSignal();
        var result = new TaskCompletionSource<Location?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var consumer = new LocationConsumer(result);
        await using var registration = timeout.Token.Register(signal.Cancel);
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            using var locationRequest = new LocationRequest.Builder(0).SetQuality((int)Quality(request.DesiredAccuracy)).Build();
            _manager.GetCurrentLocation(provider, locationRequest, signal, context.MainExecutor!, consumer);
        }
        else
        {
            _manager.GetCurrentLocation(provider, signal, context.MainExecutor!, consumer);
        }

        await using var resultOnTimeout = timeout.Token.Register(() => result.TrySetResult(null));
        var location = await result.Task;
        cancellationToken.ThrowIfCancellationRequested();
        return location;
    }

    public async Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
    {
        await EnsurePermissionAsync();
        var provider = ChooseProvider(request.DesiredAccuracy);
        if (provider is null)
        {
            return false;
        }

        lock (_lock)
        {
            if (_listener is not null)
            {
                throw new InvalidOperationException("Already listening for location updates; call StopListeningForeground first.");
            }

            _listener = new Listener(OnLocationChanged, OnProviderDisabled);
            var interval = (long)request.MinimumTime.TotalMilliseconds;
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                using var locationRequest = new LocationRequest.Builder(interval).SetQuality((int)Quality(request.DesiredAccuracy)).Build();
                _manager.RequestLocationUpdates(provider, locationRequest, context.MainExecutor!, _listener);
            }
            else
            {
                _manager.RequestLocationUpdates(provider, interval, 0f, _listener, Looper.MainLooper);
            }
        }

        return true;
    }

    public void StopListeningForeground()
    {
        lock (_lock)
        {
            if (_listener is null)
            {
                return;
            }

            _manager.RemoveUpdates(_listener);
            _listener.Dispose();
            _listener = null;
        }
    }

    public void Dispose()
    {
        StopListeningForeground();
    }

    private async Task EnsurePermissionAsync()
    {
        var isGranted = await permissions.RequestAnyAsync(LocationPermissions);
        if (!isGranted)
        {
            throw new PermissionException("The user didn't allow location, or the app's AndroidManifest.xml doesn't declare ACCESS_FINE_LOCATION and ACCESS_COARSE_LOCATION.");
        }
    }

    private string? ChooseProvider(GeolocationAccuracy accuracy)
    {
        var enabled = _manager.GetProviders(true);
        string[] fused = OperatingSystem.IsAndroidVersionAtLeast(31) ? [LocationManager.FusedProvider] : [];
        string[] preferred = accuracy is GeolocationAccuracy.High or GeolocationAccuracy.Best
            ? [LocationManager.GpsProvider, .. fused, LocationManager.NetworkProvider]
            : [.. fused, LocationManager.NetworkProvider, LocationManager.GpsProvider];
        return preferred.FirstOrDefault(enabled.Contains);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private static LocationRequestQuality Quality(GeolocationAccuracy accuracy)
    {
        return accuracy switch
        {
            GeolocationAccuracy.High or GeolocationAccuracy.Best => LocationRequestQuality.HighAccuracy,
            GeolocationAccuracy.Lowest or GeolocationAccuracy.Low => LocationRequestQuality.LowPower,
            _ => LocationRequestQuality.BalancedPowerAccuracy,
        };
    }

    private void OnLocationChanged(AndroidLocation location)
    {
        LocationChanged?.Invoke(this, new GeolocationLocationChangedEventArgs(ToLocation(location)));
    }

    private void OnProviderDisabled()
    {
        ListeningFailed?.Invoke(this, new GeolocationListeningFailedEventArgs(GeolocationError.PositionUnavailable));
    }

    private static Location ToLocation(AndroidLocation location)
    {
        return new Location(location.Latitude, location.Longitude, DateTimeOffset.FromUnixTimeMilliseconds(location.Time))
        {
            Altitude = location.HasAltitude ? location.Altitude : null,
            Accuracy = location.HasAccuracy ? location.Accuracy : null,
            VerticalAccuracy = location.HasVerticalAccuracy ? location.VerticalAccuracyMeters : null,
            Speed = location.HasSpeed ? location.Speed : null,
            Course = location.HasBearing ? location.Bearing : null,
        };
    }

    private sealed class LocationConsumer(TaskCompletionSource<Location?> result) : Java.Lang.Object, Java.Util.Functions.IConsumer
    {
        public void Accept(Java.Lang.Object? value)
        {
            result.TrySetResult(value is AndroidLocation location ? ToLocation(location) : null);
        }
    }

    private sealed class Listener(Action<AndroidLocation> changed, Action disabled) : Java.Lang.Object, ILocationListener
    {
        public void OnLocationChanged(AndroidLocation location)
        {
            changed(location);
        }

        public void OnProviderDisabled(string provider)
        {
            disabled();
        }

        public void OnProviderEnabled(string provider)
        {
        }

        public void OnStatusChanged(string? provider, Availability status, Bundle? extras)
        {
        }
    }
}
