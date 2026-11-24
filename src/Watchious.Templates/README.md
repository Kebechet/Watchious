# Kebechet.Watchious.Templates

The `dotnet new` template of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS
and watchOS, using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as
native SwiftUI on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: proof of concept.** The API can still change from one version to the next.

## Install

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

`--applicationId` sets the app's package name on Wear OS and bundle identifier on watchOS; by default it is made from
the name, here `com.companyname.mywatchapp`.

The app is one project for both watches, built with `Kebechet.Watchious.Sdk`: a counter page (`MainPage.razor`),
`WatchiousProgram.cs`, which creates the app like MAUI's `MauiProgram.cs`, and `Platforms/WearOS` and
`Platforms/watchOS`. Its target frameworks are `net10.0-android` (Wear OS), `net10.0` (the desktop preview) and, on a
Mac or with `WatchiousRemoteMac=user@host`, `net10.0-watchos`. `dotnet run -f net10.0` runs the pages on the computer.

## More

Building and running on a watch, a simulator or an emulator: "Project and packaging" in the
[Watchious README](https://github.com/Kebechet/Watchious#project-and-packaging).
