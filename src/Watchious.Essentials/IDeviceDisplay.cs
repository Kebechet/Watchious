namespace Watchious.Essentials;

// MAUI Essentials' IDeviceDisplay, for keeping the app on screen while it shows something to watch, such as a timer.
// Wear OS keeps the screen on (FLAG_KEEP_SCREEN_ON). ⚠️ watchOS can't stop the screen from dimming when the wrist goes
// down; there KeepScreenOn keeps the app running and on screen in an extended runtime session, whose kind the app
// declares in its Info.plist (WKBackgroundModes: self-care, mindfulness or physical-therapy). Without one, setting it
// throws FeatureNotSupportedException.
public interface IDeviceDisplay
{
    bool KeepScreenOn { get; set; }
}
