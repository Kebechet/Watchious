# Kebechet.Watchious.Hosts.WearOS

The Wear OS host of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: proof of concept.** The API can still change from one version to the next.

## What it is

The native Wear OS host, in Kotlin, which turns the renderer's patches into a live Compose for Wear OS view tree, and
its C# side: `WatchiousActivity`, which the app's `MainActivity` derives from, starts the app and brings taps, crown
and rotary input back. It also has the Wear OS implementations of `Kebechet.Watchious.Essentials`. The host's own
Android library (`host-release.aar`) ships in the package; the Android libraries it depends on come from Microsoft's
`Xamarin.AndroidX.*` packages, except Wear Compose Material 3, which the package bundles (see "License").

## When an app needs it

For the `net10.0-android` target framework, but an app doesn't reference it: `Kebechet.Watchious.Sdk` adds it to that
target framework at the SDK's version. Start from the template:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

## License

Watchious is MIT-licensed. This package also contains, unchanged, Google's Wear Compose Material 3 library and the
native library of AndroidX Graphics Path, both Apache-2.0: `THIRD-PARTY-NOTICES.txt` in the package lists them, with
their licenses.

## More

[Watchious README](https://github.com/Kebechet/Watchious#readme).
