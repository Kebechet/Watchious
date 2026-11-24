using System.Numerics;
using Watchious.Essentials;

namespace Watchious.Hosts.watchOS.Essentials;

// The host's sensor kinds (platforms/watchOS/Host/Sensors.swift).
internal enum WatchOSSensorKind
{
    Accelerometer = 0,
    Gyroscope = 1,
    Magnetometer = 2,
    Compass = 3,
    Barometer = 4,
    Orientation = 5,
    Pedometer = 6,
}

internal interface IWatchOSSensor
{
    void OnNativeReading(double x, double y, double z, double w);
}

// One Core Motion (or heading) stream, on while monitoring; each reading's numbers become the sensor's data.
internal sealed class WatchOSSensor<TData>(WatchOSSensorKind kind, string name, Func<double, double, double, double, TData> read, Action<TData> received)
    : IWatchOSSensor, IDisposable
{
    private const int Supported = 1;
    private const int LeftOut = 2;

    private readonly Lock _lock = new();

    public bool IsSupported => WatchOSNative.SensorIsSupported((int)kind) == Supported;

    public bool IsMonitoring { get; private set; }

    public void Start(SensorSpeed sensorSpeed)
    {
        lock (_lock)
        {
            var support = WatchOSNative.SensorIsSupported((int)kind);
            if (support == LeftOut)
            {
                throw LeftOutException();
            }

            if (support != Supported)
            {
                throw new FeatureNotSupportedException($"This watch has no {name}.");
            }

            if (IsMonitoring)
            {
                throw new InvalidOperationException($"The {name} is already monitoring; call Stop first.");
            }

            var interval = sensorSpeed switch
            {
                SensorSpeed.UI => 0.06,
                SensorSpeed.Game => 0.02,
                SensorSpeed.Fastest => 0.01,
                _ => 0.2,
            };
            WatchOSCallbacks.SetSensor(kind, this);
            var result = WatchOSNative.SensorStart((int)kind, interval);
            if (result == LeftOut)
            {
                throw LeftOutException();
            }

            IsMonitoring = true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!IsMonitoring)
            {
                return;
            }

            WatchOSNative.SensorStop((int)kind);
            IsMonitoring = false;
        }
    }

    public void OnNativeReading(double x, double y, double z, double w)
    {
        if (IsMonitoring)
        {
            received(read(x, y, z, w));
        }
    }

    public void Dispose()
    {
        Stop();
        WatchOSCallbacks.RemoveSensor(kind, this);
    }

    // The host leaves out the code of a sensor whose usage description the app's Info.plist lacks (Sensors.swift).
    private FeatureNotSupportedException LeftOutException()
    {
        var key = kind == WatchOSSensorKind.Compass ? "NSLocationWhenInUseUsageDescription" : "NSMotionUsageDescription";
        return new FeatureNotSupportedException($"The {name} is left out of this app: its Platforms/watchOS/Info.plist has no {key}, which watchOS (and App Store Connect) require of an app that uses it.");
    }
}

internal sealed class WatchOSAccelerometer : IAccelerometer, IDisposable
{
    private readonly WatchOSSensor<AccelerometerData> _sensor;

    public WatchOSAccelerometer()
    {
        _sensor = new(WatchOSSensorKind.Accelerometer, "accelerometer", Read, OnReading);
    }

    public event EventHandler<AccelerometerChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public void Start(SensorSpeed sensorSpeed)
    {
        _sensor.Start(sensorSpeed);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static AccelerometerData Read(double x, double y, double z, double w)
    {
        return new AccelerometerData(new Vector3((float)x, (float)y, (float)z));
    }

    private void OnReading(AccelerometerData data)
    {
        ReadingChanged?.Invoke(this, new AccelerometerChangedEventArgs(data));
    }
}

internal sealed class WatchOSGyroscope : IGyroscope, IDisposable
{
    private readonly WatchOSSensor<GyroscopeData> _sensor;

    public WatchOSGyroscope()
    {
        _sensor = new(WatchOSSensorKind.Gyroscope, "gyroscope", Read, OnReading);
    }

    public event EventHandler<GyroscopeChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public void Start(SensorSpeed sensorSpeed)
    {
        _sensor.Start(sensorSpeed);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static GyroscopeData Read(double x, double y, double z, double w)
    {
        return new GyroscopeData(new Vector3((float)x, (float)y, (float)z));
    }

    private void OnReading(GyroscopeData data)
    {
        ReadingChanged?.Invoke(this, new GyroscopeChangedEventArgs(data));
    }
}

internal sealed class WatchOSMagnetometer : IMagnetometer, IDisposable
{
    private readonly WatchOSSensor<MagnetometerData> _sensor;

    public WatchOSMagnetometer()
    {
        _sensor = new(WatchOSSensorKind.Magnetometer, "magnetometer", Read, OnReading);
    }

    public event EventHandler<MagnetometerChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public void Start(SensorSpeed sensorSpeed)
    {
        _sensor.Start(sensorSpeed);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static MagnetometerData Read(double x, double y, double z, double w)
    {
        return new MagnetometerData(new Vector3((float)x, (float)y, (float)z));
    }

    private void OnReading(MagnetometerData data)
    {
        ReadingChanged?.Invoke(this, new MagnetometerChangedEventArgs(data));
    }
}

internal sealed class WatchOSCompass : ICompass, IDisposable
{
    private readonly WatchOSSensor<CompassData> _sensor;

    public WatchOSCompass()
    {
        _sensor = new(WatchOSSensorKind.Compass, "compass", Read, OnReading);
    }

    public event EventHandler<CompassChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public void Start(SensorSpeed sensorSpeed)
    {
        _sensor.Start(sensorSpeed);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static CompassData Read(double x, double y, double z, double w)
    {
        return new CompassData(x);
    }

    private void OnReading(CompassData data)
    {
        ReadingChanged?.Invoke(this, new CompassChangedEventArgs(data));
    }
}

internal sealed class WatchOSBarometer : IBarometer, IDisposable
{
    private readonly WatchOSSensor<BarometerData> _sensor;

    public WatchOSBarometer()
    {
        _sensor = new(WatchOSSensorKind.Barometer, "barometer", Read, OnReading);
    }

    public event EventHandler<BarometerChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public void Start(SensorSpeed sensorSpeed)
    {
        _sensor.Start(sensorSpeed);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static BarometerData Read(double x, double y, double z, double w)
    {
        return new BarometerData(x);
    }

    private void OnReading(BarometerData data)
    {
        ReadingChanged?.Invoke(this, new BarometerChangedEventArgs(data));
    }
}

internal sealed class WatchOSOrientationSensor : IOrientationSensor, IDisposable
{
    private readonly WatchOSSensor<OrientationSensorData> _sensor;

    public WatchOSOrientationSensor()
    {
        _sensor = new(WatchOSSensorKind.Orientation, "orientation sensor", Read, OnReading);
    }

    public event EventHandler<OrientationSensorChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public void Start(SensorSpeed sensorSpeed)
    {
        _sensor.Start(sensorSpeed);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static OrientationSensorData Read(double x, double y, double z, double w)
    {
        return new OrientationSensorData(new Quaternion((float)x, (float)y, (float)z, (float)w));
    }

    private void OnReading(OrientationSensorData data)
    {
        ReadingChanged?.Invoke(this, new OrientationSensorChangedEventArgs(data));
    }
}

// Core Motion asks for Motion & Fitness when the pedometer starts. A refusal comes back as its authorization status,
// which StartAsync reads before starting again.
internal sealed class WatchOSPedometer : IPedometer, IDisposable
{
    private const int Restricted = 1;
    private const int Denied = 2;

    private readonly WatchOSSensor<PedometerData> _sensor;

    public WatchOSPedometer()
    {
        _sensor = new(WatchOSSensorKind.Pedometer, "pedometer", Read, OnReading);
    }

    public event EventHandler<PedometerChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public Task StartAsync()
    {
        var authorization = WatchOSNative.PedometerAuthorization();
        if (authorization is Restricted or Denied)
        {
            throw new PermissionException("Motion & Fitness isn't allowed for this app; the user can allow it in the watch's Settings, under Privacy & Security.");
        }

        _sensor.Start(SensorSpeed.Default);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static PedometerData Read(double x, double y, double z, double w)
    {
        return new PedometerData((long)x);
    }

    private void OnReading(PedometerData data)
    {
        ReadingChanged?.Invoke(this, new PedometerChangedEventArgs(data));
    }
}
