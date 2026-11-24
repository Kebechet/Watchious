using System.Numerics;

namespace Watchious.Essentials;

// MAUI Essentials' sensors. Start throws FeatureNotSupportedException when IsSupported is false, and
// InvalidOperationException while the sensor is already monitoring. ReadingChanged is raised on the platform's thread,
// often many times a second; a page updates itself through InvokeAsync.
public enum SensorSpeed
{
    // About 5 readings a second.
    Default,

    // About 16 a second, for the UI.
    UI,

    // About 50 a second.
    Game,

    // As fast as the watch delivers them.
    Fastest,
}

// In g, with the axes as on Android: x to the right of the screen, y up, z out of it. A watch lying flat reads
// (0, 0, 1).
public interface IAccelerometer
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    void Start(SensorSpeed sensorSpeed);

    void Stop();

    event EventHandler<AccelerometerChangedEventArgs>? ReadingChanged;
}

public readonly record struct AccelerometerData(Vector3 Acceleration);

public sealed class AccelerometerChangedEventArgs(AccelerometerData reading) : EventArgs
{
    public AccelerometerData Reading { get; } = reading;
}

// In radians per second around each axis.
public interface IGyroscope
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    void Start(SensorSpeed sensorSpeed);

    void Stop();

    event EventHandler<GyroscopeChangedEventArgs>? ReadingChanged;
}

public readonly record struct GyroscopeData(Vector3 AngularVelocity);

public sealed class GyroscopeChangedEventArgs(GyroscopeData reading) : EventArgs
{
    public GyroscopeData Reading { get; } = reading;
}

// In microteslas. Apple Watch apps have no raw magnetometer, so it isn't supported on watchOS.
public interface IMagnetometer
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    void Start(SensorSpeed sensorSpeed);

    void Stop();

    event EventHandler<MagnetometerChangedEventArgs>? ReadingChanged;
}

public readonly record struct MagnetometerData(Vector3 MagneticField);

public sealed class MagnetometerChangedEventArgs(MagnetometerData reading) : EventArgs
{
    public MagnetometerData Reading { get; } = reading;
}

// Degrees from magnetic north, clockwise.
public interface ICompass
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    void Start(SensorSpeed sensorSpeed);

    void Stop();

    event EventHandler<CompassChangedEventArgs>? ReadingChanged;
}

public readonly record struct CompassData(double HeadingMagneticNorth);

public sealed class CompassChangedEventArgs(CompassData reading) : EventArgs
{
    public CompassData Reading { get; } = reading;
}

// Air pressure in hectopascals. watchOS asks for the Motion & Fitness permission (NSMotionUsageDescription in
// Platforms/watchOS/Info.plist).
public interface IBarometer
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    void Start(SensorSpeed sensorSpeed);

    void Stop();

    event EventHandler<BarometerChangedEventArgs>? ReadingChanged;
}

public readonly record struct BarometerData(double PressureInHectopascals);

public sealed class BarometerChangedEventArgs(BarometerData reading) : EventArgs
{
    public BarometerData Reading { get; } = reading;
}

// The watch's rotation in space as a quaternion.
public interface IOrientationSensor
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    void Start(SensorSpeed sensorSpeed);

    void Stop();

    event EventHandler<OrientationSensorChangedEventArgs>? ReadingChanged;
}

public readonly record struct OrientationSensorData(Quaternion Orientation);

public sealed class OrientationSensorChangedEventArgs(OrientationSensorData reading) : EventArgs
{
    public OrientationSensorData Reading { get; } = reading;
}
