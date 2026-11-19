[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Watchious
![Last updated](https://img.shields.io/github/last-commit/Kebechet/Watchious/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

**A .NET MAUI alternative for Wear OS and watchOS, using the Blazor rendering engine.**

Write your smartwatch app once in C# and Razor. Watchious renders it as **native** SwiftUI on Apple Watch
and native Compose on Wear OS (Samsung Galaxy Watch, Google Pixel Watch and others). Your C# runs **on
the watch itself** on both platforms, so the app keeps working without the phone.

> ⚠️ **Status: early concept.** There is nothing to install yet, and no package is published. This README
> records the design, the decisions made so far and the open questions.

## Why

- .NET MAUI does not target watches.
- .NET dropped watchOS in .NET 6, when Xamarin.iOS became .NET for iOS.
- So a .NET team that wants a watch app today writes it twice: once in Swift for Apple Watch and once in
  Kotlin for Wear OS.

Watchious copies the MAUI way of doing things (single project, app builder, dependency injection, control
names), so a MAUI developer feels at home.

## How it works

Blazor's component model does not depend on HTML. Its `Renderer` diffs the component tree and emits
batches of changes. Blazor in the browser applies them to the DOM, while Blazor Server sends them over
the network. Watchious applies them to native watch UI:

```
Shared Razor components (<VerticalStackLayout>, <Label>, <Button>, <CollectionView>...)
        |  Watchious renderer (a Blazor Renderer), running on the watch
        v
compact UI changes (create / update / remove node, attach event)
        |  JNI on Wear OS / exported C functions on watchOS
        v
native host that turns the changes into a live view tree
   Wear OS -> Compose for Wear OS (Kotlin)      watchOS -> SwiftUI (Swift)
        ^
taps, crown and rotary input -> back into Blazor's dispatcher
```

Each Watchious control is a typed C# description of a native control, not a wrapper around a control
object. SwiftUI and Compose are declarative, so there is no button object to hold, only a description
to render. Because the controls are real SwiftUI and Compose controls, apps get native look,
accessibility (VoiceOver / TalkBack), crown and rotary scrolling and always-on display mode from the
platform.

App startup mirrors `MauiProgram.cs`:

```csharp
public static class WatchiousProgram
{
    public static WatchiousApp CreateWatchiousApp()
    {
        var builder = WatchiousApp.CreateBuilder();
        builder.UseWatchiousApp<App>();
        builder.Services.AddSingleton<IWorkoutService, WorkoutService>();
        return builder.Build();
    }
}
```

A page uses MAUI control names (API shape not final):

```razor
@page "/workout/{ExerciseId:int}"

<VerticalStackLayout>
    <Label Text="@exercise.Name" Style="TextStyle.Title" />
    <Label Text="@($"{reps} reps")" />
    <Button Text="+1" Clicked="() => reps++" />
</VerticalStackLayout>
```

## Platforms

| Platform | Watches | Where your C# runs | Native host |
|---|---|---|---|
| **Wear OS** | Samsung Galaxy Watch4 and newer, Google Pixel Watch, other Wear OS watches. Minimum Wear OS 3 (Android API 30). | On the watch, via .NET for Android | Compose for Wear OS, written in Kotlin |
| **watchOS** | Apple Watch. Supported models not decided yet (see open questions). | On the watch, compiled by NativeAOT into a static native library | SwiftUI, written in Swift |

> ⚠️ **.NET does not run on watchOS today.** Watchious has to make it run, and nobody has done that since
> Xamarin. The plan is NativeAOT: compile the C# into a static native library, link it into a SwiftUI
> watch app, and let Swift and C# talk through exported C functions. NativeAOT already does this on iOS
> and tvOS on the same `arm64` CPU, and since watchOS 26 Apple Watch Series 9 / Ultra 2 and newer use
> `arm64` as well. This is the riskiest part of the project, so it is researched first. If it fails,
> Watchious becomes Wear OS only.

> ⚠️ NativeAOT has no managed (C#) debugger on Apple platforms. On Apple Watch, debugging means logs and
> Xcode's native debugger.

## Planned controls

| Watchious (MAUI name) | watchOS (SwiftUI) | Wear OS (Compose) |
|---|---|---|
| `VerticalStackLayout` / `HorizontalStackLayout` | `VStack` / `HStack` | `Column` / `Row` |
| `ScrollView` | `ScrollView` | `ScalingLazyColumn` |
| `CollectionView` | `List` | `ScalingLazyColumn` |
| `Label` | `Text` | `Text` |
| `Button` | `Button` | `Button` |
| `Switch` | `Toggle` | `SwitchButton` |
| `Image` | `Image` (incl. SF Symbols) | `Image` / `Icon` |
| `ProgressBar` / `ActivityIndicator` | `ProgressView` / `Gauge` | `CircularProgressIndicator` |
| `Picker` | `Picker` | `Picker` |
| `Entry` | `TextField` | `RemoteInput` |

Platform-only controls will be available as platform-specific components (for example
`<Apple.Gauge>` or `<Wear.CurvedText>`). Styling uses semantic tokens (`TextStyle.Title`,
`Tint.Accent`) plus a few explicit values such as colour, padding and font size. There is no CSS.

## Project and packaging

Watchious copies MAUI's project model and packaging as closely as possible. Nothing is published yet.

- **Single project**, like MAUI:
  ```xml
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-watchos</TargetFrameworks>
    <UseWatchious>true</UseWatchious>
  </PropertyGroup>
  ```
  with `Platforms/Android`, `Platforms/watchOS` and shared `Resources/` (app icon, fonts, images).
- **Template:** `dotnet new watchious`.
- **Build:** `dotnet build -f net10.0-watchos` builds the complete watch app with no manual Xcode step.
  The .NET SDK does not support `net10.0-watchos` today, so Watchious provides it through its own MSBuild
  SDK, which runs NativeAOT and Apple's build tools on a Mac.
- **Companion apps:** a watch app can ship inside a .NET iPhone app (for example a MAUI app) using the
  `AdditionalWatchOSApp` item that dotnet/macios is adding in
  https://github.com/dotnet/macios/pull/27284 (still open).

| Package (working names) | MAUI equivalent | Contents |
|---|---|---|
| `Watchious.Controls` | `Microsoft.Maui.Controls` | Razor controls, navigation, app builder |
| `Watchious.Core` | `Microsoft.Maui.Core` | Renderer, UI-change protocol, native hosts |
| `Watchious.Essentials` | `Microsoft.Maui.Essentials` | Phone ↔ watch messaging, device APIs |
| `Watchious.Sdk` | the `maui` workload | `UseWatchious`, the `net10.0-watchos` target, build integration |
| `Watchious.Templates` | `Microsoft.Maui.Templates` | `dotnet new watchious` |

Versions stay at `0.x.x` until the API is stable.

## Design decisions

| Topic | Decision |
|---|---|
| Purpose | An open-source framework for other developers |
| Guiding rule | Copy the MAUI way of doing things wherever it fits a watch |
| Where C# runs | On the watch on both platforms. No phone proxy. |
| UI language | Razor only (no XAML) |
| Wear OS native layer | Compose for Wear OS host written in Kotlin |
| Target versions | .NET 10 only. Minimum Wear OS 3 (Android API 30). |
| App startup | MAUI-style `WatchiousApp.CreateBuilder()` with dependency injection |
| Control names | MAUI names: `VerticalStackLayout`, `Label`, `Switch`, `Entry`... |
| Styling | Semantic tokens plus a few explicit values. Never CSS. |
| Navigation | Blazor `@page` routes, each with a strongly typed identifier, so navigation doesn't rely on raw strings |
| Phone ↔ watch messaging | Part of Watchious: typed C# messages over WatchConnectivity (Apple) and the Data Layer (Wear OS) |
| watchOS runtime | Research running .NET on the watch now (NativeAOT static library) |
| Build | Automatic through `dotnet build`, no manual Xcode step |
| Packaging | Mirror MAUI's packages and project template. Nothing published until decided. |
| Versioning | `0.x.x` |

## Open questions

1. **Supported Apple Watches.** Running .NET on the watch needs the `arm64` architecture, which means
   watchOS 26+ on Series 9 / Ultra 2 / SE 3 and newer. Older watches (`arm64_32`) are probably impossible.
   Still to verify: whether the App Store accepts a watch app without `arm64_32`.
2. **Preview host.** A desktop window that renders the same screens, to make up for the missing C#
   debugger on Apple Watch.
3. **Shape of typed routes.** For example, generated helpers (`Navigation.GoToAsync(Routes.Workout(exerciseId: 5))`)
   or navigation by page type (`Navigation.GoToAsync<WorkoutPage>(p => p.ExerciseId = 5)`).
4. **Building for watchOS from Windows.** Mac-only, or a "pair to Mac" remote build like MAUI's.
5. **Event names.** MAUI style (`Clicked`, `Toggled`) or Blazor style (`OnClick`, `OnToggle`).

## What Watchious is not

- ⚠️ **Not a WebView or HTML renderer.** Neither Wear OS nor watchOS gives apps an embeddable WebView.
- ⚠️ **Not a way to run existing Blazor component libraries.** HTML, CSS and JS interop are not
  available. Watchious has its own set of watch controls.
- **Not for complications, Tiles or watch faces.** Those stay native: WidgetKit on watchOS and
  ProtoLayout / Watch Face Format on Wear OS.

## Roadmap

0. **watchOS runtime research:** C# compiled by NativeAOT, rendering a counter through a SwiftUI host in
   the watchOS simulator. This runs alongside step 1.
1. **Wear OS proof of concept:** the renderer plus a Compose host with `Label`, `Button` and
   `CollectionView`, running a counter app on the Wear OS emulator.
2. **SwiftUI host** with the same controls as Wear OS.
3. **Project model:** `Watchious.Sdk`, `UseWatchious`, `dotnet new watchious` and
   `dotnet build -f net10.0-watchos`.
4. **Navigation** with typed routes, and the rest of the planned controls.
5. **Messaging** between phone and watch (`Watchious.Essentials`).

## Prior art

- [Experimental Mobile Blazor Bindings](https://github.com/dotnet/MobileBlazorBindings): Microsoft's
  experiment that drove Xamarin.Forms controls from Razor components
- [BlazorBindings.Maui](https://github.com/Dreamescaper/BlazorBindings.Maui): its community successor
  for .NET MAUI
- [RazorConsole](https://github.com/RazorConsole/RazorConsole): Razor components rendered to a terminal
- Blazor Server: the same renderer, sending UI changes to a remote client

## License

[MIT](LICENSE)
