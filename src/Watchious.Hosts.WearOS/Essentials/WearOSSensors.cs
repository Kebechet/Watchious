using System.Numerics;
using Android.Content;
using Android.Hardware;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

// One Android sensor, listened to while monitoring; each SensorEvent's values become a reading.
internal sealed class WearOSSensor<TData>(Context context, SensorType type, string name, Func<IList<float>, TData> read, Action<TData> received)
    : Java.Lang.Object, ISensorEventListener
{
    private readonly SensorManager _manager = (SensorManager)context.GetSystemService(Context.SensorService)!;
    private readonly Lock _lock = new();

    public bool IsSupported => _manager.GetDefaultSensor(type) is not null;

    public bool IsMonitoring { get; private set; }

    public void Start(SensorSpeed sensorSpeed)
    {
        lock (_lock)
        {
            var sensor = _manager.GetDefaultSensor(type)
                ?? throw new FeatureNotSupportedException($"This watch has no {name}.");
            if (IsMonitoring)
            {
                throw new InvalidOperationException($"The {name} is already monitoring; call Stop first.");
            }

            var delay = sensorSpeed switch
            {
                SensorSpeed.UI => SensorDelay.Ui,
                SensorSpeed.Game => SensorDelay.Game,
                SensorSpeed.Fastest => SensorDelay.Fastest,
                _ => SensorDelay.Normal,
            };
            _manager.RegisterListener(this, sensor, delay);
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

            _manager.UnregisterListener(this);
            IsMonitoring = false;
        }
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        var values = e?.Values;
        if (values is not null && IsMonitoring)
        {
            received(read(values));
        }
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
        }

        base.Dispose(disposing);
    }
}

internal sealed class WearOSAccelerometer : IAccelerometer, IDisposable
{
    private readonly WearOSSensor<AccelerometerData> _sensor;

    public WearOSAccelerometer(Context context)
    {
        _sensor = new(context, SensorType.Accelerometer, "accelerometer", Read, OnReading);
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

    // Android reports m/s²; MAUI Essentials, and this, report g.
    private static AccelerometerData Read(IList<float> values)
    {
        return new AccelerometerData(new Vector3(values[0], values[1], values[2]) / SensorManager.GravityEarth);
    }

    private void OnReading(AccelerometerData data)
    {
        ReadingChanged?.Invoke(this, new AccelerometerChangedEventArgs(data));
    }
}

internal sealed class WearOSGyroscope : IGyroscope, IDisposable
{
    private readonly WearOSSensor<GyroscopeData> _sensor;

    public WearOSGyroscope(Context context)
    {
        _sensor = new(context, SensorType.Gyroscope, "gyroscope", Read, OnReading);
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

    private static GyroscopeData Read(IList<float> values)
    {
        return new GyroscopeData(new Vector3(values[0], values[1], values[2]));
    }

    private void OnReading(GyroscopeData data)
    {
        ReadingChanged?.Invoke(this, new GyroscopeChangedEventArgs(data));
    }
}

internal sealed class WearOSMagnetometer : IMagnetometer, IDisposable
{
    private readonly WearOSSensor<MagnetometerData> _sensor;

    public WearOSMagnetometer(Context context)
    {
        _sensor = new(context, SensorType.MagneticField, "magnetometer", Read, OnReading);
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

    private static MagnetometerData Read(IList<float> values)
    {
        return new MagnetometerData(new Vector3(values[0], values[1], values[2]));
    }

    private void OnReading(MagnetometerData data)
    {
        ReadingChanged?.Invoke(this, new MagnetometerChangedEventArgs(data));
    }
}

// Android has no compass sensor: the heading is the azimuth of the rotation vector, which fuses the magnetometer with
// the accelerometer and gyroscope.
internal sealed class WearOSCompass : ICompass, IDisposable
{
    private readonly WearOSSensor<CompassData> _sensor;

    public WearOSCompass(Context context)
    {
        _sensor = new(context, SensorType.RotationVector, "compass", Read, OnReading);
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

    private static CompassData Read(IList<float> values)
    {
        var rotation = new float[9];
        SensorManager.GetRotationMatrixFromVector(rotation, [.. values]);
        var orientation = new float[3];
        SensorManager.GetOrientation(rotation, orientation);
        var degrees = orientation[0] * 180.0 / Math.PI;
        return new CompassData((degrees + 360) % 360);
    }

    private void OnReading(CompassData data)
    {
        ReadingChanged?.Invoke(this, new CompassChangedEventArgs(data));
    }
}

internal sealed class WearOSBarometer : IBarometer, IDisposable
{
    private readonly WearOSSensor<BarometerData> _sensor;

    public WearOSBarometer(Context context)
    {
        _sensor = new(context, SensorType.Pressure, "barometer", Read, OnReading);
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

    private static BarometerData Read(IList<float> values)
    {
        return new BarometerData(values[0]);
    }

    private void OnReading(BarometerData data)
    {
        ReadingChanged?.Invoke(this, new BarometerChangedEventArgs(data));
    }
}

internal sealed class WearOSOrientationSensor : IOrientationSensor, IDisposable
{
    private readonly WearOSSensor<OrientationSensorData> _sensor;

    public WearOSOrientationSensor(Context context)
    {
        _sensor = new(context, SensorType.RotationVector, "orientation sensor", Read, OnReading);
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

    // getQuaternionFromVector returns w, x, y, z.
    private static OrientationSensorData Read(IList<float> values)
    {
        var quaternion = new float[4];
        SensorManager.GetQuaternionFromVector(quaternion, [.. values]);
        return new OrientationSensorData(new Quaternion(quaternion[1], quaternion[2], quaternion[3], quaternion[0]));
    }

    private void OnReading(OrientationSensorData data)
    {
        ReadingChanged?.Invoke(this, new OrientationSensorChangedEventArgs(data));
    }
}

// The step counter counts from the watch's start; the steps since StartAsync are the difference to its first reading.
internal sealed class WearOSPedometer : IPedometer, IDisposable
{
    private readonly WearOSSensor<long> _sensor;
    private readonly WearOSPermissions _permissions;
    private long? _firstCount;

    public WearOSPedometer(Context context, WearOSPermissions permissions)
    {
        _permissions = permissions;
        _sensor = new(context, SensorType.StepCounter, "step counter", Read, OnCount);
    }

    public event EventHandler<PedometerChangedEventArgs>? ReadingChanged;

    public bool IsSupported => _sensor.IsSupported;

    public bool IsMonitoring => _sensor.IsMonitoring;

    public async Task StartAsync()
    {
        if (!IsSupported)
        {
            throw new FeatureNotSupportedException("This watch has no step counter.");
        }

        var isAllowed = await _permissions.RequestAnyAsync(Android.Manifest.Permission.ActivityRecognition);
        if (!isAllowed)
        {
            throw new PermissionException("Physical activity isn't allowed for this app, or its manifest doesn't declare ACTIVITY_RECOGNITION.");
        }

        _firstCount = null;
        _sensor.Start(SensorSpeed.Default);
    }

    public void Stop()
    {
        _sensor.Stop();
    }

    public void Dispose()
    {
        _sensor.Dispose();
    }

    private static long Read(IList<float> values)
    {
        return (long)values[0];
    }

    private void OnCount(long count)
    {
        _firstCount ??= count;
        ReadingChanged?.Invoke(this, new PedometerChangedEventArgs(new PedometerData(count - _firstCount.Value)));
    }
}
