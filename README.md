[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Watchious
![Last updated](https://img.shields.io/github/last-commit/Kebechet/Watchious/main?label=last%20updated)
[![Twitter](https://img.shields.io/twitter/url/https/twitter.com/samuel_sidor.svg?style=social&label=Follow%20samuel_sidor)](https://x.com/samuel_sidor)

**A .NET MAUI alternative for Wear OS and watchOS, using the Blazor rendering engine.**

Write your smartwatch app once in C# and Razor. Watchious renders it as **native** SwiftUI on Apple Watch
and native Compose on Wear OS (Samsung Galaxy Watch, Google Pixel Watch and others). Your C# runs **on
the watch itself** on both platforms, so the app keeps working without the phone.

> ⚠️ **Status: proof of concept.** The same Razor counter app runs as native UI on a real Apple Watch SE 3
> (watchOS 27) and a Galaxy Watch7, and in the watchOS simulator and the Wear OS emulator. The watchOS device
> build also passes App Store validation. There is nothing to install yet, and no package is
> published. This README records the design, the decisions made so far and the open questions.

## Why

- .NET MAUI does not target watches.
- .NET dropped watchOS in .NET 6, when Xamarin.iOS became .NET for iOS.
- So a .NET team that wants a watch app today writes it twice: once in Swift for Apple Watch and once in
  Kotlin for Wear OS.

Watchious copies the MAUI way of doing things (single project, app builder, dependency injection, platform
code in C#), so a MAUI developer feels at home. Its controls use SwiftUI's names, the vocabulary of watch UI,
which Compose for Wear OS largely shares.

## How it works

Blazor's component model does not depend on HTML. Its `Renderer` diffs the component tree and emits
batches of changes. Blazor in the browser applies them to the DOM, while Blazor Server sends them over
the network. Watchious applies them to native watch UI:

```
Shared Razor components (<VStack>, <Label>, <Button>, <List>...)
        |  Watchious renderer (a Blazor Renderer), running on the watch
        v
binary patches of what changed (create / update / insert / remove / move node, set event)
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

A page uses SwiftUI's control names with Blazor-style events, binding and `@page` routes (API shape not final):

```razor
@page "/workout/{ExerciseId:int}"
@inject INavigation Navigation

<VStack>
    <Label Style="TextStyle.Title">@exercise.Name</Label>
    <Label>@reps reps</Label>
    <Toggle Text="Warm-up" @bind-IsOn="isWarmup" />
    <Button Text="+1" OnClick="() => reps++" />
    <Button Text="Next" OnClick="GoToNextAsync" />
</VStack>

@code {
    private Task GoToNextAsync() => Navigation.GoToAsync(Routes.Workout(exerciseId: ExerciseId + 1));
}
```

`Routes.Workout(...)` is generated from the `@page` directive by a source generator, so navigation is
checked by the compiler instead of relying on raw strings. The pages form a stack shown with each platform's
own navigation: SwiftUI's `NavigationStack` with its back button and edge swipe on watchOS, swipe-to-dismiss and
the back button on Wear OS. Pages below the top one keep their state.

## Platforms

| Platform | Watches | Where your C# runs | Native host |
|---|---|---|---|
| **Wear OS** | Samsung Galaxy Watch4 and newer, Google Pixel Watch, other Wear OS watches. Minimum Wear OS 3 (Android API 30). | On the watch, via .NET for Android | Compose for Wear OS, written in Kotlin |
| **watchOS** | Apple Watch Series 9 / Ultra 2 / SE 3 and newer, on watchOS 27+ (the `arm64` watches). Older `arm64_32` watches are not supported. | On the watch, compiled by NativeAOT into a static native library | SwiftUI, written in Swift |

> ⚠️ **.NET does not support watchOS.** Watchious makes it run itself: NativeAOT compiles the C# into a
> static native library, which is linked into a SwiftUI watch app, and Swift and C# talk through exported C
> functions. NativeAOT already does this on iOS and tvOS on the same `arm64` CPU, and since watchOS 26 Apple
> Watch Series 9 / Ultra 2 and newer use `arm64` as well. This works on a real Apple Watch SE 3.

> ⚠️ **On a real Apple Watch, a null dereference in C# ends the app** instead of throwing
> `NullReferenceException`. watchOS stops any app that installs a handler for a fatal signal ("sigaction on
> fatal signals is not supported"), which is how the .NET runtime turns memory faults into exceptions, so
> Watchious keeps the runtime from installing it (`platforms/watchOS/Host/FatalSignals.c`). The watchOS
> simulator behaves the same way.

> ⚠️ NativeAOT has no managed (C#) debugger on Apple platforms. On Apple Watch, debugging means logs and
> Xcode's native debugger.

## Controls

✅ = built.

| Watchious (SwiftUI's names) | watchOS (SwiftUI) | Wear OS (Compose) |
|---|---|---|
| ✅ `VStack` / `HStack` | `VStack` / `HStack` | `Column` / `Row` |
| ✅ `ScrollView` | `ScrollView` | `Column` with rotary scrolling |
| ✅ `List` | `List` | `TransformingLazyColumn` |
| ✅ `Label` | `Text` | `Text` |
| ✅ `Button` | `Button` | `Button` |
| ✅ `Toggle` (with a `Text` label) | `Toggle` | `SwitchButton` |
| ✅ `Image` (app images; SF Symbols and Material icons later) | `Image` | `Image` |
| ✅ `ProgressView` | `ProgressView` | `CircularProgressIndicator` / `LinearProgressIndicator` |
| ✅ `Picker` | `Picker` | `Picker` |
| ✅ `TextField` | `TextField` | `BasicTextField` with the system keyboard |

> ⚠️ **SwiftUI's `Text` is `Label` in Watchious.** Razor reserves the `<text>` tag, in any casing, inside `@if`
> and `@foreach` blocks: there a `<Text>` component would fail to compile or quietly turn into plain markup, which
> the Razor team keeps by design (https://github.com/dotnet/razor/issues/12620). So the text control keeps MAUI's
> name. Its content is the text, as in SwiftUI: `<Label>Count: @count</Label>`.

Platform-only controls will be available as platform-specific components (for example
`<Apple.Gauge>` or `<Wear.CurvedText>`). Styling uses semantic tokens (`TextStyle.Title`,
`Tint.Accent`) plus a few explicit values such as colour, padding and font size. There is no CSS.

## Watch APIs

`Watchious.Essentials` has the watch APIs as C# services, like MAUI Essentials but injected:

```razor
@inject IHaptics Haptics
@inject IPreferences Preferences

<Button Text="Done" OnClick="() => Haptics.Play(HapticFeedback.Success)" />
```

| Service | watchOS | Wear OS |
|---|---|---|
| `IHaptics` | `WKInterfaceDevice.play`, one haptic per meaning | the closest predefined vibration |
| `IDeviceInfo` | `WKInterfaceDevice`, model identifier | `Build`, round screen |
| `IBattery` | `WKInterfaceDevice` battery (unknown in the simulator) | the battery broadcast |
| `IConnectivity` | `NWPathMonitor` | `ConnectivityManager` |
| `IPreferences` | `UserDefaults` (suite `watchious`) | `SharedPreferences` (file `watchious`) |

Each host registers its platform's implementations before the app's `CreateWatchiousApp` runs, so an app (or a
test) can replace any of them with its own registration. WatchKit, which .NET for iOS has no bindings for,
is reached through small C functions in the SwiftUI host (`platforms/watchOS/Host/Essentials.swift`).

## Preview and inspector

NativeAOT has no C# debugger on Apple Watch, so Watchious brings two development pages:

- **Preview:** `dotnet run -f net10.0` (or the `net10.0` framework picked in Visual Studio) runs the app on the
  desktop and opens a page that renders it in a Wear OS or Apple Watch frame. The page applies the same patches
  as the watch hosts and sends clicks, toggles and text back as events. The app runs in the desktop process, so
  breakpoints in its pages and services work. The watch APIs have stand-ins (haptics are logged).
- **Inspector:** `dotnet run -f net10.0 -- --inspector` starts a page that mirrors an app running on a watch,
  simulator or emulator, next to its live UI tree and a log of patches, events and errors. Clicks on the mirror
  happen in the app on the watch. The app connects when it is started with the inspector's address:
  - Wear OS (debuggable builds only): `adb reverse tcp:5292 tcp:5292`, then
    `adb shell am start -n <package>/<activity> --es watchious_inspector ws://localhost:5292/inspector/app`.
  - watchOS: the launch argument `--watchious-inspector=ws://<computer>:5292/inspector/app`, for example
    `dotnet build -t:Run -f net10.0-watchos -p:WatchiousLaunchArguments=...`. A watch on the network needs the
    inspector started with `--urls=http://0.0.0.0:5292`.

## Project and packaging

Watchious copies MAUI's project model and packaging as closely as possible. Nothing is published yet:
`dotnet pack` puts the packages in `artifacts/packages`.

- **Single project**, like MAUI, through the Watchious MSBuild SDK:
  ```xml
  <Project Sdk="Watchious.Sdk/0.1.0-preview.1">
    <PropertyGroup>
      <TargetFrameworks>net11.0-android;net11.0-watchos</TargetFrameworks>
      <OutputType>Exe</OutputType>
      <ApplicationId>com.companyname.myapp</ApplicationId>
    </PropertyGroup>
  </Project>
  ```
  with shared `.razor` pages, shared images in `Resources/Images` (`<Image Source="logo" />` shows
  `logo.png`), `Platforms/WearOS` (`MainActivity`, `AndroidManifest.xml`, Android resources in `Resources/`) and
  `Platforms/watchOS` (`Info.plist`, `Assets.xcassets` with the app icon, `Entitlements.plist` for capabilities such as
  HealthKit, or MAUI's `CodesignEntitlements`). It is an MSBuild SDK, like Uno's
  `Uno.Sdk`, rather than a `UseWatchious` property, because the watchOS target framework has to exist before
  NuGet restore runs. The SDK adds the Watchious packages itself, and `WatchiousProgram.CreateWatchiousApp()`
  creates the app (the `WatchiousAppFactory` property can name another method).
- **Template:** `dotnet new watchious`.
- **Build and run:** `dotnet build -t:Run -f net11.0-watchos` builds the watch app with no manual Xcode step
  and starts it in the watchOS simulator. `-r ios-arm64 -p:WatchiousDevice=<UDID>` installs it on a watch,
  signed with MAUI's `CodesignKey` and `CodesignProvision`. `-f net11.0-android` works as for any .NET for
  Android app.
  - .NET has no watchOS target, so the SDK builds a `net*-watchos` project with .NET for iOS (NativeAOT),
    relabels the native code for watchOS and links it into the SwiftUI host. C# gets Microsoft's bindings for
    Apple's frameworks that way, while NuGet still treats the project as `net*-watchos`.
  - **From Windows:** set `WatchiousRemoteMac=user@host` and the watchOS target builds on that Mac over ssh,
    like MAUI's "Pair to Mac", and the app comes back. Windows needs no iOS workload for it.
- **Release:** from CI pipelines, like a MAUI app: `dotnet publish -f net11.0-android` gives the Play Store
  bundle, and `dotnet build -f net11.0-watchos -r ios-arm64 -p:BuildIpa=true` on a macOS runner gives the
  App Store `.ipa`, inside the watch-only container Apple requires (`WatchiousContainerProvision` is the
  container's profile). Signing comes from the pipeline's secrets.
- The repository itself still builds with .NET 10 (`net10.0-*`) until .NET 11 is installed on its
  development machines.
- **Companion apps:** a watch app can ship inside a .NET iPhone app (for example a MAUI app) using the
  `AdditionalWatchOSApp` item that dotnet/macios is adding in
  https://github.com/dotnet/macios/pull/27284 (still open).

| Package (working names) | MAUI equivalent | Contents |
|---|---|---|
| `Watchious.Controls` | `Microsoft.Maui.Controls` | Razor controls, navigation, app builder |
| `Watchious.Core` | `Microsoft.Maui.Core` | Renderer and UI-change protocol |
| `Watchious.Hosts.WearOS`, `Watchious.Hosts.watchOS` | the platform parts of `Microsoft.Maui.Controls` | The native hosts; the SDK adds them |
| `Watchious.Essentials` | `Microsoft.Maui.Essentials` | The common watch APIs as services; the hosts add the platform implementations |
| `Watchious.Messaging` | none (closest: [Plugin.Maui.WearableMessaging](https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging) on the phone) | Phone ↔ watch messaging. Optional. |
| `Watchious.Health` | [`Kebechet.Maui.Health`](https://github.com/Kebechet/Maui.Health) (not part of MAUI itself) | Heart rate and workout sessions (HealthKit / Health Services). Optional. |
| `Watchious.Preview` | none | The desktop preview and the live inspector |
| `Watchious.Sdk` | the `maui` workload | `<Project Sdk="Watchious.Sdk">`: the `net11.0-watchos` target, building, running, remote Mac builds and App Store packaging |
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
| Target versions | .NET 11 and later. Minimum Wear OS 3 (Android API 30), built for 32-bit and 64-bit ARM: Samsung's Galaxy Watches run 32-bit Android even on 64-bit chips, and .NET builds 64-bit only by default. Minimum watchOS 27: below it the App Store requires an `arm64_32` slice (ITMS-90733), which NativeAOT cannot build. |
| Target frameworks | Wear OS uses the standard `net11.0-android`, so the .NET for Android build, debugger and Android NuGet packages keep working. A custom `net11.0-wearos` would lose all of them. watchOS gets Watchious's own `net11.0-watchos`, because .NET has none and there are no watchOS packages to lose. |
| Naming | Watchious's own folders, projects and packages say `WearOS` and `watchOS`, never `Android` or `iOS`. Those names mean the phone, where the messaging code also runs. Like MAUI (`Platforms/iOS`, `DevicePlatform.watchOS`), a platform name on its own keeps Apple's casing (`Watchious.Hosts.watchOS`); inside a longer identifier it is Pascal-cased, as in .NET's `OperatingSystem.IsWatchOS()` (`WatchOSBridge`). Names are lowercase only where the tool requires it: the `net11.0-watchos` target framework, compiler target triples and Gradle project names. |
| Supported Apple Watches | Series 9 / Ultra 2 / SE 3 and newer (`arm64`). No `arm64_32` watches. |
| App startup | MAUI-style `WatchiousApp.CreateBuilder()` with dependency injection |
| Control names | SwiftUI's vocabulary: `VStack`, `Toggle`, `List`, `TextField`... Not MAUI's names, except `Label` for SwiftUI's `Text`, because Razor reserves `<text>`. Every common UI element is wrapped with its common parameters, with SwiftUI's defaults (stacks center their children and leave the spacing to the platform). |
| Toggle label | `Toggle` has a `Text` parameter. Both native toggles carry their own label, which looks native and is read together with the switch by VoiceOver and TalkBack. |
| Events and binding | Blazor style: `OnClick`, `@bind-IsOn` |
| App state | Blazor style: state in components with `@bind`, shared logic in services through dependency injection |
| Watch APIs | All common watch APIs wrapped as C# services in dependency injection, unified across the platforms as far as possible, in the style of [`Kebechet.Maui.RevenueCat.InAppBilling`](https://github.com/Kebechet/Maui.RevenueCat.InAppBilling). A feature only one platform has stays in the API and throws on the other. |
| Platform-specific code | C# in `Platforms/WearOS` and `Platforms/watchOS`, like MAUI. Wear OS: Android and AndroidX APIs through .NET for Android. watchOS: Apple's frameworks (HealthKit, Foundation, CoreMotion...) through Microsoft's .NET for iOS bindings, which run on the watch (shown on an Apple Watch SE 3). Watch-only Apple APIs that .NET for iOS lacks get small bindings in Watchious. |
| Health and workouts | In scope later, as a separate, optional `Watchious.Health` package after messaging |
| Styling | Semantic tokens plus a few explicit values. Never CSS. |
| Navigation | Blazor `@page` routes plus source-generated typed helpers: `Navigation.GoToAsync(Routes.Workout(exerciseId: 5))`. A stack of pages, shown with each platform's own navigation and back gestures. |
| Phone ↔ watch messaging | A separate, optional `Watchious.Messaging` package: typed C# messages over WatchConnectivity (Apple) and the Data Layer (Wear OS), ideally compatible with Plugin.Maui.WearableMessaging on the phone (https://github.com/Kebechet/Watchious/issues/4) |
| watchOS runtime | NativeAOT, built as .NET for iOS (`ios-arm64`) and relabelled for watchOS. Microsoft's runtime glue (`libxamarin`) is started the way its own `main` starts it, minus the iPhone app. ⚠️ That relies on internal start-up functions of .NET for iOS, which may change between versions. |
| Build | Automatic through `dotnet build`, no manual Xcode step. watchOS builds run on a Mac, or from Windows on a remote Mac like MAUI's "Pair to Mac". Releases come from CI pipelines, like a MAUI app. |
| Debugging | A desktop preview of the same screens (`dotnet run -f net10.0`) and a live inspector of apps running on watches |
| Testing | Watch simulators and emulators first, real watches later |
| Packaging | Mirror MAUI's packages and project template. `Watchious.Sdk` is an MSBuild SDK, like Uno's `Uno.Sdk`. Nothing published until decided. |
| Versioning | `0.x.x` |

## Open questions

1. ✅ **App Store and `arm64`-only watch apps.** Watchious does not support `arm64_32`, so its watch apps
   ship without that architecture. Answered: App Store validation accepts such an app when its minimum is
   watchOS 27, and rejects it below that (ITMS-90733).

## What Watchious is not

- ⚠️ **Not a WebView or HTML renderer.** Neither Wear OS nor watchOS gives apps an embeddable WebView.
- ⚠️ **Not a way to run existing Blazor component libraries.** HTML, CSS and JS interop are not
  available. Watchious has its own set of watch controls.
- **Not for complications, Tiles or watch faces.** Those stay native: WidgetKit on watchOS and
  ProtoLayout / Watch Face Format on Wear OS.

## Roadmap

0. ✅ **watchOS runtime research:** C# compiled by NativeAOT, rendering a counter through a SwiftUI host in
   the watchOS simulator and on a real Apple Watch SE 3. The device build, signed and packaged as a
   watch-only app, passes App Store validation.
1. ✅ **Wear OS proof of concept:** the renderer plus a Compose host with `Label`, `Button` and a list,
   running a counter app on the Wear OS emulator and a Galaxy Watch7. The list came later, with step 5.
2. ✅ **SwiftUI host** with the same controls as Wear OS. From now on, each new control is added to both
   hosts together.
3. ✅ **Project model:** `Watchious.Sdk`, `dotnet new watchious` and `dotnet build -f net11.0-watchos` on top
   of .NET for iOS, App Store packaging, and remote builds from Windows on a Mac. Installing on a real Apple
   Watch through `-t:Run` is still to be tried.
4. ✅ **Patch-based protocol:** after each render, the renderer sends the host only what changed, as one
   binary buffer of patches (create, set attribute or event, set text, insert, remove, move), instead of the
   whole UI tree as JSON (https://github.com/Kebechet/Watchious/issues/2). Both hosts apply them to an
   observable node tree, so SwiftUI and Compose redraw only the changed controls. A counter tap now sends
   29 bytes instead of 569, and adding a row to a 50-row list 243 bytes instead of 13,965. Measurements on a
   Galaxy Watch7 are still to do.
5. ✅ **Controls and navigation:** the controls under SwiftUI's names, the rest of the planned controls including
   `List`, and navigation with typed routes. ✅ All controls in the table above, and navigation (`@page` routes,
   `INavigation`, generated `Routes` helpers, native back on both platforms). Still to do: SF Symbols and
   Material icons for `Image`.
6. **Watch APIs** (`Watchious.Essentials`): the common watch features as unified C# services, with
   platform C# as the escape hatch. Done so far: haptics, device info, battery, connectivity and preferences.
   Next: notifications, location, sensors, secure storage, always-on (ambient) mode and app lifecycle events.
7. **Messaging** between phone and watch (`Watchious.Messaging`, optional,
   https://github.com/Kebechet/Watchious/issues/4).
8. ✅ **Preview host:** the same screens on the desktop, for debugging in Visual Studio (`Watchious.Preview`,
   see "Preview and inspector").
9. ✅ **Live inspector:** a local page that shows a running watch app's UI tree, updates and events, and can send
   it events (https://github.com/Kebechet/Watchious/issues/5).
10. **Health and workouts** (`Watchious.Health`, optional).

## Prior art

- [Experimental Mobile Blazor Bindings](https://github.com/dotnet/MobileBlazorBindings): Microsoft's
  experiment that drove Xamarin.Forms controls from Razor components
- [BlazorBindings.Maui](https://github.com/Dreamescaper/BlazorBindings.Maui): its community successor
  for .NET MAUI
- [RazorConsole](https://github.com/RazorConsole/RazorConsole): Razor components rendered to a terminal
- Blazor Server: the same renderer, sending UI changes to a remote client
- [BlazorNative](https://github.com/MarcelRoozekrans/BlazorNative): Blazor components compiled by NativeAOT
  and shown as native Android and iOS views, sent as typed patches through a C interface and laid out by
  Yoga. Phones only. Its patch protocol is the reference for Watchious's
  (https://github.com/Kebechet/Watchious/issues/2).
- [Codename One](https://www.codenameone.com/blog/native-apple-watch-and-wear/): Java apps for Apple Watch
  and Wear OS from one code base. On watchOS it draws with Core Graphics inside a SwiftUI shell instead of
  using SwiftUI controls.
- [Uno Platform 6.7](https://platform.uno/blog/uno-platform-6-7/): Wear OS as an opt-in for an Android
  project (AndroidX Wear libraries and manifest entries). The watch UI is still yours to build, and there
  is no watchOS.

## License

[MIT](LICENSE)
