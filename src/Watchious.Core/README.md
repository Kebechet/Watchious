# Kebechet.Watchious.Core

The renderer of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: preview.** The API can still change from one version to the next.

## What it is

A Blazor `Renderer` that doesn't render HTML: it turns the app's Razor components into binary patches of what changed
(create, update, insert, remove and move a node, set an event), which the native hosts apply to their SwiftUI or Compose
view tree. Taps, crown and rotary input come back into Blazor's dispatcher as events.

## When an app needs it

Always, but an app doesn't reference it: it comes with `Kebechet.Watchious.Controls`, which `Kebechet.Watchious.Sdk`
adds to every app at the SDK's version. Start from the template:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

## More

"How it works" in the [Watchious README](https://github.com/Kebechet/Watchious#how-it-works).
