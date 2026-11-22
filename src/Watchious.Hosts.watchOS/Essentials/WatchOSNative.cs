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
