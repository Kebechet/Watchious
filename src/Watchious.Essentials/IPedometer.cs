namespace Watchious.Essentials;

// The steps the watch counts while monitoring (MAUI has no pedometer): CMPedometer on watchOS, the step counter sensor
// on Wear OS. StartAsync asks for the permission on first use, Motion & Fitness on watchOS and physical activity
// (ACTIVITY_RECOGNITION, which the app's manifest declares) on Wear OS, and throws PermissionException when it's denied.
// ReadingChanged then reports the steps since the start whenever the count changes, on the platform's thread.
public interface IPedometer
{
    bool IsSupported { get; }

    bool IsMonitoring { get; }

    Task StartAsync();

    void Stop();

    event EventHandler<PedometerChangedEventArgs>? ReadingChanged;
}

public readonly record struct PedometerData(long NumberOfSteps);

public sealed class PedometerChangedEventArgs(PedometerData reading) : EventArgs
{
    public PedometerData Reading { get; } = reading;
}
