using Android.Content;
using Android.OS;
using Android.Runtime;
using Watchious.Essentials;

namespace Watchious.Hosts.WearOS.Essentials;

internal sealed class WearOSHaptics : IHaptics
{
    private readonly Vibrator? _vibrator;

    public WearOSHaptics(Context context)
    {
        _vibrator = OperatingSystem.IsAndroidVersionAtLeast(31)
            ? context.GetSystemService(Context.VibratorManagerService)?.JavaCast<VibratorManager>().DefaultVibrator
            : context.GetSystemService(Context.VibratorService)?.JavaCast<Vibrator>();
    }

    public void Play(HapticFeedback feedback)
    {
        if (_vibrator is null || !_vibrator.HasVibrator)
        {
            return;
        }

        _vibrator.Vibrate(VibrationEffect.CreatePredefined(ToEffect(feedback)));
    }

    // Wear OS has no haptic per meaning, unlike watchOS, so each maps to the closest predefined vibration.
    private static int ToEffect(HapticFeedback feedback)
    {
        return feedback switch
        {
            HapticFeedback.Click => VibrationEffect.EffectClick,
            HapticFeedback.DirectionUp or HapticFeedback.DirectionDown => VibrationEffect.EffectTick,
            HapticFeedback.Success or HapticFeedback.Stop => VibrationEffect.EffectDoubleClick,
            _ => VibrationEffect.EffectHeavyClick,
        };
    }
}
