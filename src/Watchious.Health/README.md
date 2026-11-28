# Kebechet.Watchious.Health

Health for [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: preview.** The API can still change from one version to the next.

## What it is

What a watch app does with health data: permissions and live workouts with heart rate, with HealthKit on watchOS and
Health Services on Wear OS. It has [Kebechet.Maui.Health](https://github.com/Kebechet/Maui.Health)'s service names
(`IHealthService`, `Activity.Start/Pause/Resume/End`) and its types, from the MAUI-free `Kebechet.Types.Health`
(`WorkoutDto`, `HeartRateDto`, `ActivityType`, `WorkoutSession`...), so a watch workout reaches a phone app that uses
Maui.Health as the same type. A workout keeps running when the user leaves the app.

## When an app needs it

When the watch app uses health data. The SDK doesn't add it: the app references it at the SDK's version,
`$(WatchiousVersion)`, and registers it:

```xml
<ItemGroup>
  <PackageReference Include="Kebechet.Watchious.Health" Version="$(WatchiousVersion)" />
</ItemGroup>
```

⚠️ **With Central Package Management** (a `Directory.Packages.props`), leave `Version` off and give
`Kebechet.Watchious.Health` a `PackageVersion` there at the SDK's version, written out (a library project has no
`$(WatchiousVersion)`). Nothing reports it left at an older version, so an update of the SDK changes it too.

```csharp
using Watchious.Health;

builder.Services.AddWatchiousHealth();
```

Then a page or service injects `IHealthService`.

- watchOS: the app needs the HealthKit entitlement (`Platforms/watchOS/Entitlements.plist`) and
  `NSHealthShareUsageDescription` and `NSHealthUpdateUsageDescription` in `Platforms/watchOS/Info.plist`. The SDK
  compiles the package's Swift (`HealthKitWorkout.swift`) into the app's SwiftUI host.
- Wear OS: the package's Android library declares the permissions it needs, and asks for them itself.

## License

Watchious is MIT-licensed. This package also contains, unchanged, Google's Health Services client library and its
protos (Apache-2.0) and the protobuf runtime it repackages (BSD-3-Clause): `THIRD-PARTY-NOTICES.txt` in the package
lists them, with their licenses.

## More

"Phone messaging and health" in the
[Watchious README](https://github.com/Kebechet/Watchious#phone-messaging-and-health).
