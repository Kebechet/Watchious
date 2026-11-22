using Android.Content;
using Android.OS;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

internal sealed class WearOSBattery(Context context) : IBattery
{
    public double? ChargeLevel
    {
        get
        {
            using var status = ReadBatteryStatus();
            var level = status?.GetIntExtra(BatteryManager.ExtraLevel, -1) ?? -1;
            var scale = status?.GetIntExtra(BatteryManager.ExtraScale, -1) ?? -1;
            return level < 0 || scale <= 0 ? null : (double)level / scale;
        }
    }

    public BatteryState State
    {
        get
        {
            using var status = ReadBatteryStatus();
            return (BatteryStatus)(status?.GetIntExtra(BatteryManager.ExtraStatus, -1) ?? -1) switch
            {
                BatteryStatus.Charging => BatteryState.Charging,
                BatteryStatus.Discharging or BatteryStatus.NotCharging => BatteryState.Discharging,
                BatteryStatus.Full => BatteryState.Full,
                _ => BatteryState.Unknown,
            };
        }
    }

    // The battery broadcast is sticky: registering without a receiver just returns the latest one.
    private Intent? ReadBatteryStatus()
    {
        using var filter = new IntentFilter(Intent.ActionBatteryChanged);
        return context.RegisterReceiver(null, filter);
    }
}
