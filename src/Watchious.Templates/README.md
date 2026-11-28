# Kebechet.Watchious.Templates

The `dotnet new` template of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS
and watchOS, using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as
native SwiftUI on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: preview.** The API can still change from one version to the next.

## Install

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

`--applicationId` sets the app's package name on Wear OS; by default it is made from the name, here
`com.companyname.mywatchapp`. On watchOS the watch app's bundle identifier is the applicationId plus `.watchkitapp`
(`com.companyname.mywatchapp.watchkitapp`; `WatchiousBundleIdentifier` sets another one), and its provisioning profiles
are for that identifier. The applicationId itself is the bundle identifier of the App Store package's watch-only
container.

The project names `Kebechet.Watchious.Sdk` of this template's version,
`<Project Sdk="Kebechet.Watchious.Sdk/<version>">`: all Watchious packages share one version, and the SDK adds the
others at its own. `--sdk-version` names another version:

```bash
dotnet new watchious -n MyWatchApp --sdk-version <version>
```

The app is one project for both watches, built with `Kebechet.Watchious.Sdk`: a counter page (`MainPage.razor`),
`WatchiousProgram.cs`, which creates the app like MAUI's `MauiProgram.cs`, and `Platforms/WearOS` and
`Platforms/watchOS`. Its target frameworks are `net10.0-android` (Wear OS), `net10.0` (the desktop preview) and, on a
Mac or with `WatchiousRemoteMac=user@host`, `net10.0-watchos`. `dotnet run -f net10.0` runs the pages on the computer.

## More

Building and running on a watch, a simulator or an emulator: "Project and packaging" in the
[Watchious README](https://github.com/Kebechet/Watchious#project-and-packaging).
