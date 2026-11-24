# Kebechet.Watchious.Preview

The desktop preview and inspector of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: proof of concept.** The API can still change from one version to the next.

## What it is

NativeAOT has no C# debugger on Apple Watch, so this package runs a watch app on the computer instead:

- **Preview:** `dotnet run -f net10.0` runs the app's C# on the computer, where a .NET debugger works, and opens
  `http://localhost:5290`, a page that renders it in a Wear OS or Apple Watch frame. The page applies the same patches as
  the watch hosts and sends taps back as events, so breakpoints in the pages and services work.
- **Inspector:** `dotnet run -f net10.0 -- --inspector` opens `http://localhost:5292`, a page that mirrors the app
  running on a watch, simulator or emulator, with its live UI tree and a log of patches, events and errors.

`-- --no-browser` starts either without opening a browser, and `-- --urls=<url>` changes its address.

## When an app needs it

For the `net10.0` target framework, the preview, but an app doesn't reference it: `Kebechet.Watchious.Sdk` adds it to
that target framework at the SDK's version, with the program that starts it. Start from the template, which has the
`net10.0` target framework:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

## More

How an app on a watch connects to the inspector, and live mode: "Preview, inspector and live mode" in the
[Watchious README](https://github.com/Kebechet/Watchious#preview-inspector-and-live-mode).
