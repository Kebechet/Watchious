# Kebechet.Watchious.Hosts.watchOS

The watchOS host bridge of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: preview.** The API can still change from one version to the next.

## What it is

The C# side of the watchOS host. The SwiftUI host calls it through C functions that the app exports: it starts the
app on the watch, hands the renderer's patches to the host and brings the host's events back. It also has the watchOS
implementations of `Kebechet.Watchious.Essentials`. The SwiftUI host itself (Swift and C sources) is in
`Kebechet.Watchious.Sdk`, which compiles it into the watch app; the two must be the same version.

## When an app needs it

For the `net10.0-watchos` target framework, but an app doesn't reference it: `Kebechet.Watchious.Sdk` adds it to that
target framework at exactly the SDK's version. Start from the template:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

## More

How a watchOS app is built from .NET for iOS: "Project and packaging" in the
[Watchious README](https://github.com/Kebechet/Watchious#project-and-packaging).
