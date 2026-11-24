using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Watchious.Hosts.watchOS.Essentials;

// The Swift host's callbacks for the watch APIs, registered once before the app starts. Native callbacks are static, so
// each reaches the service that registered itself here. An exception from an app's event handler is logged rather than
// thrown, because one that escapes into Swift ends the app.
internal static unsafe class WatchOSCallbacks
{
    private static readonly ConcurrentDictionary<long, TaskCompletionSource<int>> Pending = new();
    private static readonly ConcurrentDictionary<int, IWatchOSSensor> Sensors = new();
    private static long _lastToken;
    private static int _isRegistered;

    public static WatchOSAppLifecycle? AppLifecycle { get; set; }

    public static WatchOSAmbientMode? AmbientMode { get; set; }

    public static WatchOSGeolocation? Geolocation { get; set; }

    public static void Register()
    {
        if (Interlocked.Exchange(ref _isRegistered, 1) == 1)
        {
            return;
        }

        WatchOSNative.EssentialsSetCallbacks(&OnCompleted, &OnAppState, &OnAmbient, &OnLocation, &OnLocationFailed, &OnSensor);
    }

    public static long NextToken()
    {
        return Interlocked.Increment(ref _lastToken);
    }

    // Starts a native call that completes through OnCompleted with the token it is given.
    public static Task<int> RunAsync(Action<long> start)
    {
        var token = NextToken();
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Pending[token] = result;
        start(token);
        return result.Task;
    }

    public static void SetSensor(WatchOSSensorKind kind, IWatchOSSensor sensor)
    {
        Sensors[(int)kind] = sensor;
    }

    // Only while it is still the registered one: a newer sensor of the same kind may have taken its place.
    public static void RemoveSensor(WatchOSSensorKind kind, IWatchOSSensor sensor)
    {
        Sensors.TryRemove(new KeyValuePair<int, IWatchOSSensor>((int)kind, sensor));
    }

    [UnmanagedCallersOnly]
    private static void OnCompleted(long token, int result)
    {
        if (Pending.TryRemove(token, out var pending))
        {
            pending.TrySetResult(result);
        }
    }

    [UnmanagedCallersOnly]
    private static void OnAppState(int state)
    {
        Raise(() => AppLifecycle?.OnNativeState(state));
    }

    [UnmanagedCallersOnly]
    private static void OnAmbient(int isAmbient)
    {
        Raise(() => AmbientMode?.OnNativeAmbient(isAmbient != 0));
    }

    [UnmanagedCallersOnly]
    private static void OnLocation(long token, double* values)
    {
        var location = WatchOSGeolocation.ToLocation(new ReadOnlySpan<double>(values, 8));
        Raise(() => Geolocation?.OnNativeLocation(token, location));
    }

    [UnmanagedCallersOnly]
    private static void OnLocationFailed(long token, int code)
    {
        Raise(() => Geolocation?.OnNativeFailed(token, code));
    }

    [UnmanagedCallersOnly]
    private static void OnSensor(int kind, double x, double y, double z, double w)
    {
        if (Sensors.TryGetValue(kind, out var sensor))
        {
            Raise(() => sensor.OnNativeReading(x, y, z, w));
        }
    }

    private static void Raise(Action raise)
    {
        try
        {
            raise();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Watchious: an event handler of a watch API threw: {exception}");
        }
    }
}
