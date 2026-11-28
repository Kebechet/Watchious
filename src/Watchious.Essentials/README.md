# Kebechet.Watchious.Essentials

The watch APIs of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: preview.** The API can still change from one version to the next.

## What it is

The watch APIs as C# services, with MAUI Essentials' names, the same on Wear OS and watchOS: `IHaptics`,
`IDeviceInfo`, `IBattery`, `IConnectivity`, `IPreferences`, `ISecureStorage`, `IAppLifecycle`, `IAmbientMode`,
`ILocalNotifications`, `IGeolocation`, the sensors (`IAccelerometer`, `IGyroscope`, `IOrientationSensor`,
`IMagnetometer`, `ICompass`, `IBarometer`, `IPedometer`), `IDeviceDisplay`, `IAppInfo`, `IFileSystem` and
`ITextToSpeech`. The hosts register the platform implementations; a page injects the service:

```razor
@using Watchious.Essentials
@inject IHaptics Haptics

<Button Text="Done" OnClick="() => Haptics.Play(HapticFeedback.Success)" />
```

## When an app needs it

Always, but an app doesn't reference it: it comes with the hosts and the preview, which `Kebechet.Watchious.Sdk` adds
to every app at the SDK's version. Start from the template:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

## More

What each service is on each platform, and the permissions and Info.plist keys an app declares for them: "Watch APIs"
in the [Watchious README](https://github.com/Kebechet/Watchious#watch-apis).
