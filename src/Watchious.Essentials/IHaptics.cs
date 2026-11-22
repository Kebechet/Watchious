namespace Watchious.Essentials;

public interface IHaptics
{
    // watchOS plays its own haptic for each kind; Wear OS plays the closest predefined vibration.
    void Play(HapticFeedback feedback);
}

public enum HapticFeedback
{
    Click,
    Success,
    Failure,
    Notification,
    Start,
    Stop,
    Retry,
    DirectionUp,
    DirectionDown,
}
