[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Watchious
![Last updated](https://img.shields.io/github/last-commit/Kebechet/Watchious/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

**A .NET MAUI alternative for Wear OS and watchOS, using the Blazor rendering engine.**

Write your smartwatch UI once in C# and Razor, and render it as **native** SwiftUI on Apple Watch and
native Compose on Wear OS (Samsung Galaxy Watch, Google Pixel Watch and others).

> ⚠️ **Status: early concept.** There is nothing to install yet. This README describes the design and the
> plan.

## Why

- .NET MAUI does not target watches.
- .NET dropped watchOS in .NET 6, when Xamarin.iOS became .NET for iOS.
- So a .NET team that wants a watch app today writes it twice: once in Swift for Apple Watch and once in
  Kotlin for Wear OS.

Watchious lets you keep C#, Razor and your existing services and view-models, and still get a native
watch UI on both platforms.

## How it works

Blazor's component model does not depend on HTML. Its `Renderer` diffs the component tree and emits
batches of changes. Blazor in the browser applies them to the DOM, while Blazor Server sends them over
the network. Watchious applies them to native watch UI:

```
Shared Razor components (<VStack>, <Text>, <Button>, <ScrollList>...)
        |  Watchious renderer (a Blazor Renderer)
        v
compact UI changes (create / update / remove node, attach event)
        |  JNI on Android / C functions on Apple
        v
native host that turns the changes into a live view tree
   Wear OS -> Compose for Wear OS      watchOS -> SwiftUI
        ^
taps, crown and rotary input -> back into Blazor's dispatcher
```

Each Watchious component is a typed C# description of a native control, not a wrapper around a
control object. SwiftUI and Compose are declarative, so there is no button object to hold, only a
description to render. Because the controls are real SwiftUI and Compose controls, apps get native
look, accessibility (VoiceOver / TalkBack), crown and rotary scrolling and always-on display mode from
the platform.

```razor
<VStack>
    <Text Value="@exercise.Name" Style="TextStyle.Title" />
    <Text Value="@($"{reps} reps")" />
    <Button Label="+1" OnClick="() => reps++" />
</VStack>
```

## Platforms

| Platform | Watches | Where your C# runs |
|---|---|---|
| **Wear OS** | Samsung Galaxy Watch4 and newer, Google Pixel Watch, other Wear OS watches | On the watch, via .NET for Android |
| **watchOS** | Apple Watch | On the paired iPhone. The watch shows the UI and sends input back over WatchConnectivity. |

> ⚠️ **watchOS has no .NET runtime.** In phone-hosted mode the watch UI needs the iPhone to be reachable.
> Running .NET on the watch itself is a research goal. It became more realistic with watchOS 26, which
> moved Apple Watch Series 9 / Ultra 2 and newer to the same `arm64` architecture as the iPhone.

## Planned components

| Watchious | watchOS (SwiftUI) | Wear OS (Compose) |
|---|---|---|
| `VStack` / `HStack` | `VStack` / `HStack` | `Column` / `Row` |
| `ScrollList` | `List` | `ScalingLazyColumn` |
| `Text` | `Text` | `Text` |
| `Button` | `Button` | `Button` |
| `Toggle` | `Toggle` | `SwitchButton` |
| `Icon` | SF Symbols | Material icons |
| `Progress` | `Gauge` / `ProgressView` | `CircularProgressIndicator` |
| `Picker` | `Picker` | `Picker` |
| `Navigation` | `NavigationStack` | `SwipeDismissableNavHost` |
| `TextInput` | `TextField` | `RemoteInput` |

Platform-only controls will be available as platform-specific components (for example
`<Apple.Gauge>` or `<Wear.CurvedText>`). Styling uses semantic tokens (`TextStyle.Title`,
`Tint.Accent`) instead of CSS, so each platform keeps its own native look.

## What Watchious is not

- ⚠️ **Not a WebView or HTML renderer.** Neither Wear OS nor watchOS gives apps an embeddable WebView.
- ⚠️ **Not a way to run existing Blazor component libraries.** HTML, CSS and JS interop are not
  available. Watchious has its own set of watch components.
- **Not for complications, Tiles or watch faces.** Those stay native: WidgetKit on watchOS and
  ProtoLayout / Watch Face Format on Wear OS.

## Roadmap

1. **Wear OS proof of concept:** the renderer plus a Compose host with `Text`, `Button` and
   `ScrollList`, running a counter app on the Wear OS emulator.
2. **watchOS:** a SwiftUI host and the phone-hosted mode over WatchConnectivity.
3. **More components:** the rest of the planned set, plus navigation and crown / rotary input.
4. **Research:** running .NET directly on Apple Watch.

## Prior art

- [Experimental Mobile Blazor Bindings](https://github.com/dotnet/MobileBlazorBindings): Microsoft's
  experiment that drove Xamarin.Forms controls from Razor components
- [BlazorBindings.Maui](https://github.com/Dreamescaper/BlazorBindings.Maui): its community successor
  for .NET MAUI
- [RazorConsole](https://github.com/RazorConsole/RazorConsole): Razor components rendered to a terminal
- Blazor Server: the same renderer, sending UI changes to a remote client

## License

[MIT](LICENSE)
