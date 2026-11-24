using System.Runtime.InteropServices;

namespace Watchious.Hosts.watchOS.Essentials;

// The watch APIs' C functions in the SwiftUI host (platforms/watchOS/Host/Essentials.swift). They are linked into
// the same executable, so they are P/Invoked from "__Internal", which .NET for iOS's NativeAOT build links
// directly. WatchKit is watch-only, so .NET for iOS has no bindings for it.
internal static unsafe partial class WatchOSNative
{
    public const int DeviceModel = 0;
    public const int DeviceSystemVersion = 1;

    [LibraryImport("__Internal", EntryPoint = "watchious_haptics_play")]
    public static partial void HapticsPlay(int feedback);

    // Returns a UTF-8 string allocated with malloc; TakeString frees it.
    [LibraryImport("__Internal", EntryPoint = "watchious_device_string")]
    public static partial nint DeviceString(int field);

    // platforms/watchOS/Host/AppServices.swift: 0 name, 1 bundle identifier, 2 version, 3 build, 10 the app data folder,
    // 11 the cache folder. A string to TakeString.
    [LibraryImport("__Internal", EntryPoint = "watchious_app_string")]
    public static partial nint AppString(int field);

    // 0 when done, 2 when the app's Info.plist declares no extended runtime session.
    [LibraryImport("__Internal", EntryPoint = "watchious_display_keep_on")]
    public static partial int DisplayKeepOn(int isOn);

    // platforms/watchOS/Host/Speech.swift: completes with 1 when spoken, 0 when stopped. A negative pitch or volume keeps
    // the voice's own.
    [LibraryImport("__Internal", EntryPoint = "watchious_speech_speak", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void SpeechSpeak(string text, string? language, float pitch, float volume, long token);

    [LibraryImport("__Internal", EntryPoint = "watchious_speech_stop")]
    public static partial void SpeechStop();

    // One voice per line: its language, name and identifier, separated by tabs. A string to TakeString.
    [LibraryImport("__Internal", EntryPoint = "watchious_speech_voices")]
    public static partial nint SpeechVoices();

    // CMAuthorizationStatus: 0 not asked yet, 1 restricted, 2 denied, 3 allowed; -1 when the host has no pedometer.
    [LibraryImport("__Internal", EntryPoint = "watchious_pedometer_authorization")]
    public static partial int PedometerAuthorization();

    // From 0 to 1, or -1 when unknown.
    [LibraryImport("__Internal", EntryPoint = "watchious_battery_level")]
    public static partial float BatteryLevel();

    // 0 unknown, 1 charging, 2 unplugged, 3 full.
    [LibraryImport("__Internal", EntryPoint = "watchious_battery_state")]
    public static partial int BatteryState();

    // 0 unknown, 1 none, 2 internet.
    [LibraryImport("__Internal", EntryPoint = "watchious_network_status")]
    public static partial int NetworkStatus();

    [LibraryImport("__Internal", EntryPoint = "watchious_preferences_get", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint PreferencesGet(string key);

    [LibraryImport("__Internal", EntryPoint = "watchious_preferences_set", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void PreferencesSet(string key, string value);

    [LibraryImport("__Internal", EntryPoint = "watchious_preferences_remove", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void PreferencesRemove(string key);

    [LibraryImport("__Internal", EntryPoint = "watchious_preferences_contains", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int PreferencesContains(string key);

    [LibraryImport("__Internal", EntryPoint = "watchious_preferences_clear")]
    public static partial void PreferencesClear();

    // platforms/watchOS/Host/EssentialsCallbacks.swift: completion (token, result), app state, dimming, position
    // (token, 8 doubles), position failure (token, code) and sensor reading (kind, x, y, z, w).
    [LibraryImport("__Internal", EntryPoint = "watchious_essentials_set_callbacks")]
    public static partial void EssentialsSetCallbacks(
        delegate* unmanaged<long, int, void> completion,
        delegate* unmanaged<int, void> appState,
        delegate* unmanaged<int, void> ambient,
        delegate* unmanaged<long, double*, void> location,
        delegate* unmanaged<long, int, void> locationFailed,
        delegate* unmanaged<int, double, double, double, double, void> sensor);

    // 0 active, 1 inactive, 2 background, as Watchious.Essentials.AppState.
    [LibraryImport("__Internal", EntryPoint = "watchious_app_state")]
    public static partial int AppState();

    [LibraryImport("__Internal", EntryPoint = "watchious_is_luminance_reduced")]
    public static partial int IsLuminanceReduced();

    [LibraryImport("__Internal", EntryPoint = "watchious_secure_get", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint SecureGet(string key);

    // The Keychain's OSStatus: 0 when stored.
    [LibraryImport("__Internal", EntryPoint = "watchious_secure_set", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SecureSet(string key, string value);

    [LibraryImport("__Internal", EntryPoint = "watchious_secure_remove", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SecureRemove(string key);

    [LibraryImport("__Internal", EntryPoint = "watchious_secure_remove_all")]
    public static partial void SecureRemoveAll();

    [LibraryImport("__Internal", EntryPoint = "watchious_notifications_request_permission")]
    public static partial void NotificationsRequestPermission(long token);

    [LibraryImport("__Internal", EntryPoint = "watchious_notifications_show", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void NotificationsShow(int id, string title, string? body, double delaySeconds, long token);

    [LibraryImport("__Internal", EntryPoint = "watchious_notifications_cancel")]
    public static partial void NotificationsCancel(int id);

    [LibraryImport("__Internal", EntryPoint = "watchious_notifications_cancel_all")]
    public static partial void NotificationsCancelAll();

    // 1 when location services are on, 0 when not, 2 when the host was built without location, for an app whose
    // Info.plist has no NSLocationWhenInUseUsageDescription.
    [LibraryImport("__Internal", EntryPoint = "watchious_location_is_supported")]
    public static partial int LocationIsSupported();

    // Completes with 1 allowed, 0 denied, 2 no NSLocationWhenInUseUsageDescription in the app's Info.plist.
    [LibraryImport("__Internal", EntryPoint = "watchious_location_request_authorization")]
    public static partial void LocationRequestAuthorization(long token);

    // Core Location's desired accuracy in metres; -1 is the best it can.
    [LibraryImport("__Internal", EntryPoint = "watchious_location_request")]
    public static partial void LocationRequest(long token, double accuracy);

    [LibraryImport("__Internal", EntryPoint = "watchious_location_cancel")]
    public static partial void LocationCancel(long token);

    [LibraryImport("__Internal", EntryPoint = "watchious_location_start")]
    public static partial void LocationStart(double accuracy);

    [LibraryImport("__Internal", EntryPoint = "watchious_location_stop")]
    public static partial void LocationStop();

    // Fills 8 doubles as a position update; 0 when there is no position yet.
    [LibraryImport("__Internal", EntryPoint = "watchious_location_last_known")]
    public static partial int LocationLastKnown(double* values);

    // 1 when the watch has it, 0 when not, 2 when the host was built without it (the compass without
    // NSLocationWhenInUseUsageDescription, the barometer without NSMotionUsageDescription in the app's Info.plist).
    [LibraryImport("__Internal", EntryPoint = "watchious_sensor_is_supported")]
    public static partial int SensorIsSupported(int kind);

    // 0 when started, 2 when the host was built without it.
    [LibraryImport("__Internal", EntryPoint = "watchious_sensor_start")]
    public static partial int SensorStart(int kind, double intervalSeconds);

    [LibraryImport("__Internal", EntryPoint = "watchious_sensor_stop")]
    public static partial void SensorStop(int kind);

    // The value of the --watchious-inspector=<url> launch argument, or null.
    [LibraryImport("__Internal", EntryPoint = "watchious_inspector_url")]
    public static partial nint InspectorUrl();

    public static string? TakeString(nint utf8)
    {
        if (utf8 == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(utf8);
        }
        finally
        {
            NativeMemory.Free((void*)utf8);
        }
    }
}
