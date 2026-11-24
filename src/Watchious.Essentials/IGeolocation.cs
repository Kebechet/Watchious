namespace Watchious.Essentials;

// MAUI Essentials' IGeolocation, for the watch's own position: one reading, or updates while the app is in the
// foreground. Each call asks for the location permission first if the user hasn't answered yet, and throws
// PermissionException when it's denied. The app declares the permission: NSLocationWhenInUseUsageDescription in
// Platforms/watchOS/Info.plist, ACCESS_FINE_LOCATION and ACCESS_COARSE_LOCATION in the Wear OS manifest.
// LocationChanged and ListeningFailed are raised on the platform's thread; a page updates itself through InvokeAsync.
public interface IGeolocation
{
    bool IsSupported { get; }

    bool IsListeningForeground { get; }

    Task<Location?> GetLastKnownLocationAsync();

    // Null when no position could be found in time.
    Task<Location?> GetLocationAsync(GeolocationRequest? request = null, CancellationToken cancellationToken = default);

    Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request);

    void StopListeningForeground();

    event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;

    event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed;
}

// Accuracies in metres, altitude above sea level, speed in metres per second, course in degrees from true north.
public sealed record Location(double Latitude, double Longitude, DateTimeOffset Timestamp)
{
    public double? Altitude { get; init; }

    public double? Accuracy { get; init; }

    public double? VerticalAccuracy { get; init; }

    public double? Speed { get; init; }

    public double? Course { get; init; }
}

// Default is Medium. A watch away from its phone may find no position below High: Wear OS turns its GPS on only for High
// and Best, so a workout asks for one of those.
public enum GeolocationAccuracy
{
    Default,
    Lowest,
    Low,
    Medium,
    High,
    Best,
}

public sealed class GeolocationRequest
{
    public GeolocationAccuracy DesiredAccuracy { get; init; } = GeolocationAccuracy.Default;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class GeolocationListeningRequest
{
    public GeolocationAccuracy DesiredAccuracy { get; init; } = GeolocationAccuracy.Default;

    public TimeSpan MinimumTime { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed class GeolocationLocationChangedEventArgs(Location location) : EventArgs
{
    public Location Location { get; } = location;
}

public enum GeolocationError
{
    PositionUnavailable,
    Unauthorized,
}

public sealed class GeolocationListeningFailedEventArgs(GeolocationError error) : EventArgs
{
    public GeolocationError Error { get; } = error;
}
