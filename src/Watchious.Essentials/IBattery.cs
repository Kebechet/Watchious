namespace Watchious.Essentials;

public interface IBattery
{
    // From 0 to 1, or null when the platform can't tell (as in the watchOS simulator).
    double? ChargeLevel { get; }

    BatteryState State { get; }
}

public enum BatteryState
{
    Unknown,
    Charging,
    Discharging,
    Full,
}
