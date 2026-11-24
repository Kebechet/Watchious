[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/kebechet)

# Watchious
[![watchOS canary](https://github.com/Kebechet/Watchious/actions/workflows/watchos-canary.yml/badge.svg?branch=main)](https://github.com/Kebechet/Watchious/actions/workflows/watchos-canary.yml)
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
| ✅ `Image` (app images and system icons) | `Image`, `Image(systemName:)` for SF Symbols | `Image`, `Icon` for Material icons |
| ✅ `ProgressView` | `ProgressView` | `CircularProgressIndicator` / `LinearProgressIndicator` |
| ✅ `Picker` | `Picker` | `Picker`, which the crown turns once it is touched |
| ✅ `DigitalCrownRotation` (the crown turns a value) | `.digitalCrownRotation` on its content | its content with `onRotaryScrollEvent` |
| ✅ `TextField` | `TextField` | `BasicTextField` with the system keyboard |

> ⚠️ **SwiftUI's `Text` is `Label` in Watchious.** Razor reserves the `<text>` tag, in any casing, inside `@if`
> and `@foreach` blocks: there a `<Text>` component would fail to compile or quietly turn into plain markup, which
> the Razor team keeps by design (https://github.com/dotnet/razor/issues/12620). So the text control keeps MAUI's
> name. Its content is the text, as in SwiftUI: `<Label>Count: @count</Label>`.

A test (`HostCoverageTests`) fails when a control, attribute, event or enum value of the C# controls appears nowhere
in the SwiftUI host, the Compose host or the browser preview. It reads each host's sources as text, so it catches a
name a host lacks altogether, not one that is read on another control only. An element a host doesn't know, such as a misspelled
tag in the markup, is logged, and in a Debug build it shows a ⚠️ with its name in its place.

`DigitalCrownRotation` is SwiftUI's `digitalCrownRotation` modifier as a wrapper: the crown turns `Value` from
`From` through `Through` in steps of `By` (default 1), clockwise up, and its content shows the value. On Wear OS the
crown or the rotating bezel turns it, one step per detent. On a screen that scrolls, the crown scrolls until the user
touches the content, as with a `Picker`:

```razor
<DigitalCrownRotation @bind-Value="weight" From="0" Through="200" By="2.5">
    <Label Style="TextStyle.Title">@weight kg</Label>
</DigitalCrownRotation>
```

`Image` also shows system icons in each platform's own style: `<Image Icon="Icon.Heart" />` is the SF Symbol
`heart.fill` on watchOS and the Material icon `Favorite` (Rounded, the style of Wear OS) on Wear OS. An icon takes the
colour of the text around it, and on watchOS its size too (Wear OS draws it 24 dp); `Width` or `Height` sizes it, and
one of them sizes both sides, as icons are square. `AppleSymbol` and `MaterialIcon` name any other icon of one platform,
and win over `Icon` there: `<Image Icon="Icon.Walk" AppleSymbol="figure.hiking" MaterialIcon="Hiking" />`. When that
name isn't found, `Icon` stands in. A name a platform doesn't have is logged, and a debug build shows a ⚠️ with the name
in its place. The preview draws
the icons with Google's Material Symbols font when it is online, and shows an `AppleSymbol` by its name.

<details>
<summary>The shared icons</summary>

| `Icon` | SF Symbol | Material icon (Rounded) |
|---|---|---|
| `Heart` | `heart.fill` | `Favorite` |
| `Star` | `star.fill` | `Star` |
| `Bell` | `bell.fill` | `Notifications` |
| `Home` | `house.fill` | `Home` |
| `Person` | `person.fill` | `Person` |
| `Settings` | `gearshape.fill` | `Settings` |
| `Info` | `info.circle.fill` | `Info` |
| `Warning` | `exclamationmark.triangle.fill` | `Warning` |
| `Search` | `magnifyingglass` | `Search` |
| `Lock` | `lock.fill` | `Lock` |
| `Plus` | `plus` | `Add` |
| `Minus` | `minus` | `Remove` |
| `Checkmark` | `checkmark` | `Check` |
| `Close` | `xmark` | `Close` |
| `Edit` | `pencil` | `Edit` |
| `Delete` | `trash.fill` | `Delete` |
| `Refresh` | `arrow.clockwise` | `Refresh` |
| `Share` | `square.and.arrow.up` | `Share` |
| `ChevronLeft` | `chevron.left` | `ChevronLeft` |
| `ChevronRight` | `chevron.right` | `ChevronRight` |
| `ChevronUp` | `chevron.up` | `KeyboardArrowUp` |
| `ChevronDown` | `chevron.down` | `KeyboardArrowDown` |
| `Play` | `play.fill` | `PlayArrow` |
| `Pause` | `pause.fill` | `Pause` |
| `Stop` | `stop.fill` | `Stop` |
| `Microphone` | `mic.fill` | `Mic` |
| `Speaker` | `speaker.wave.2.fill` | `AutoMirrored.VolumeUp` |
| `Music` | `music.note` | `MusicNote` |
| `Phone` | `phone.fill` | `Phone` |
| `Message` | `message.fill` | `AutoMirrored.Chat` |
| `Location` | `mappin.and.ellipse` | `LocationOn` |
| `Calendar` | `calendar` | `CalendarToday` |
| `Clock` | `clock.fill` | `Schedule` |
| `Timer` | `timer` | `Timer` |
| `Alarm` | `alarm.fill` | `Alarm` |
| `Run` | `figure.run` | `AutoMirrored.DirectionsRun` |
| `Walk` | `figure.walk` | `AutoMirrored.DirectionsWalk` |
| `Bike` | `figure.outdoor.cycle` | `AutoMirrored.DirectionsBike` |
| `Swim` | `figure.pool.swim` | `Pool` |
| `Dumbbell` | `dumbbell.fill` | `FitnessCenter` |
| `Flame` | `flame.fill` | `LocalFireDepartment` |
| `Bolt` | `bolt.fill` | `Bolt` |
| `Drop` | `drop.fill` | `WaterDrop` |
| `Moon` | `moon.fill` | `Bedtime` |
| `Sun` | `sun.max.fill` | `WbSunny` |
| `Battery` | `battery.100` | `BatteryFull` |
| `Wifi` | `wifi` | `Wifi` |
| `List` | `list.bullet` | `AutoMirrored.List` |

</details>

⚠️ **List each `MaterialIcon` the app uses in the Wear OS project**, as `<WatchiousMaterialIcon Include="Hiking" />`.
Release builds run R8 (`AndroidLinkTool=r8`, which Watchious.Sdk turns on), and R8 keeps only the icons that code
names: a `MaterialIcon` is found by its name at run time, so without the item it works in a debug build and is gone in
a release build.

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
| `ISecureStorage` | the Keychain | AES-GCM with a key in the Android Keystore |
| `IAppLifecycle` | SwiftUI's scene phase | the activity's lifecycle |
| `IAmbientMode` | `isLuminanceReduced` | `AmbientLifecycleObserver`, turned on when the app uses the service |
| `ILocalNotifications` | `UNUserNotificationCenter`, shown while the app is open too | a notification channel; `AlarmManager` for a later time |
| `IGeolocation` | `CLLocationManager` | `LocationManager`, GPS for `High` and `Best` |
| `IAccelerometer`, `IGyroscope`, `IOrientationSensor` | Core Motion | `SensorManager` |
| `IMagnetometer` | not on Apple Watch (`IsSupported` is false) | `SensorManager` |
| `ICompass` | Core Location's heading | the rotation vector's azimuth |
| `IBarometer` | `CMAltimeter` | the pressure sensor |

The names and shapes are MAUI Essentials' where it has the API (`IGeolocation`, `ISecureStorage`, `IAccelerometer`,
`SensorSpeed`...). A sensor or service a watch doesn't have says so in `IsSupported`, and using it anyway throws
`FeatureNotSupportedException`. Location and notifications ask for their permission on first use and throw
`PermissionException` when it's denied. The app declares what each platform needs:
`NSLocationWhenInUseUsageDescription` and, for the barometer, `NSMotionUsageDescription` in
`Platforms/watchOS/Info.plist`; `ACCESS_FINE_LOCATION` and `ACCESS_COARSE_LOCATION` in the Wear OS manifest. ⚠️ The
watchOS host builds its location and compass code only when `NSLocationWhenInUseUsageDescription` is there, and its
barometer only with `NSMotionUsageDescription`: App Store Connect refuses an app whose code uses them without the text
(ITMS-90683). Without the key, `IsSupported` is false and using the API throws `FeatureNotSupportedException` naming
it. Events
come on the platform's thread, often many times a second for a sensor: a page updates itself through `InvokeAsync`,
and unsubscribes in `Dispose`.

Each host registers its platform's implementations before the app's `CreateWatchiousApp` runs, so an app (or a
test) can replace any of them with its own registration. WatchKit, which .NET for iOS has no bindings for,
is reached through small C functions in the SwiftUI host (`platforms/watchOS/Host/Essentials.swift`).

## Phone messaging and health

Two optional packages, each registered by the app:

```csharp
builder.Services.AddPhoneMessaging();   // Watchious.Messaging: inject IPhoneMessaging
builder.Services.AddWatchiousHealth();  // Watchious.Health: inject IHealthService
```

- **`Watchious.Messaging`** talks to a MAUI phone app that uses
  [Plugin.Maui.WearableMessaging](https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging)
  unchanged: messages, messages with an answer, and the application context. An Android phone app answers with a
  message whose `_replyToId` is the message's `_requestId`, since the plugin has no `ReplyHandler` on Android. On
  Wear OS the package uses the Wearable Data Layer (Play Services) in the plugin's format, and advertises the
  `wearable_app` capability the plugin looks for;
  the phone and watch apps must share the applicationId and signing key. On watchOS it uses WatchConnectivity; the
  watch app must be the iPhone app's companion, installed from inside it (see "Companion apps").
  `samples/Watchious.Samples.Phone` is a MAUI phone app with the plugin for testing it; with
  `--watchious-phone=check,hello,ask,context` it presses its own buttons and logs to the console (not on iOS yet:
  the arguments of `xcrun devicectl device process launch` don't reach `Environment.GetCommandLineArgs()`).
  Messages, answers and the application context go both ways between a Galaxy Watch7 and a Pixel 6, and between an
  Apple Watch SE 3 and an iPhone SE.
  ⚠️ **On iOS, don't await the plugin's `SendMessageAsync` yet.** In 0.6.5 it completes only when sending fails, so
  for a delivered message the `await` never returns
  (https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging/issues/5, fixed in
  https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging/pull/6). The phone sample doesn't await it
  on iOS until a release has that fix.
- **`Watchious.Health`** does what a watch does with health data: permissions and live workouts with heart
  rate, with [Kebechet.Maui.Health](https://github.com/Kebechet/Maui.Health)'s service names (`IHealthService`,
  `Activity.Start/Pause/Resume/End`). Its data types are Maui.Health's own (`WorkoutDto`, `HeartRateDto`,
  `ActivityType`, `WorkoutSession`...), from the MAUI-free `Kebechet.Types.Health`, so a watch workout reaches a
  phone app that uses Maui.Health as the same type. Wear OS uses Health Services, and asks for its permissions
  itself; watchOS uses HealthKit (`HKWorkoutSession` with `HKLiveWorkoutBuilder`) and needs the HealthKit
  entitlement and `NSHealthShareUsageDescription` / `NSHealthUpdateUsageDescription` in `Platforms/watchOS`.
  A workout keeps running when the user leaves the app: on watchOS its `HKWorkoutSession` keeps the app running, and
  on Wear OS a foreground service does, with an ongoing activity on the watch face that brings the user back. When the
  app starts again, `Activity.GetActive()` returns the running workout, with its title and pauses; its heart rate
  summary counts what the app saw while it was open. After a crash, watchOS hands the app its running session back
  (`recoverActiveWorkoutSession`): on an Apple Watch SE 3, an app killed during a run started again with the workout
  and its heart rate, and could resume and end it. Wear OS's Health Services ends an exercise when its app's process
  dies (seen on the emulator), so there the workout is over.

Each package brings its native part: a Java module on Wear OS, built with Gradle next to the host, and a Swift file
that Watchious.Sdk compiles into the watchOS host (`WatchiousWatchOSHostSource`).

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
    `adb shell am start -S -n <package>/<activity> --es watchious_inspector http://localhost:5292/inspector/app`
    (`-S` restarts the app if it is open, since it reads the address when it starts).
  - watchOS: the launch argument `--watchious-inspector=http://<computer>:5292/inspector/app`, for example
    `dotnet build -t:Run -f net10.0-watchos -p:WatchiousLaunchArguments=...`. A watch, or a simulator on another
    computer (the Mac of a remote build), needs the inspector started with `--urls=http://0.0.0.0:5292`.
  - The app talks to the inspector with plain HTTP requests, because a real Apple Watch gives apps URLSession's
    HTTP(S) but no sockets, and WebSockets only while an app streams audio or runs a call
    (https://developer.apple.com/documentation/technotes/tn3135-low-level-networking-on-watchos); an earlier
    WebSocket version was refused on an Apple Watch SE 3. Debug watch builds allow plain HTTP on the local network
    (`NSAllowsLocalNetworking`). The HTTP version works on an Apple Watch SE 3, with clicks from the page and live
    sensor values.
  - ⚠️ **Keep the watch's screen on.** watchOS runs an app only while its screen is on: an app started while the
    screen is off stays in the background, suspended, and reaches the inspector once the user raises the wrist.

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
  - An Apple Watch is reached over a Wi-Fi tunnel that drops while the watch sleeps. Run waits for the watch,
    keeps the tunnel up during the install, and tries install and launch again while the watch can't be reached
    (up to 5 times, 10 s apart), so a dozing watch only needs a wake-up. Signing and provisioning errors fail at
    once. ⚠️ The app starts in the background, suspended, when the watch's screen is off, and runs once the user
    raises the wrist.
  - .NET has no watchOS target, so the SDK builds a `net*-watchos` project with .NET for iOS (NativeAOT),
    relabels the native code for watchOS and links it into the SwiftUI host. C# gets Microsoft's bindings for
    Apple's frameworks that way, while NuGet still treats the project as `net*-watchos`.
  - **From Windows:** set `WatchiousRemoteMac=user@host` and the watchOS target builds on that Mac over ssh,
    like MAUI's "Pair to Mac", and the app comes back. Windows needs no iOS workload for it. ⚠️ From Git Bash, set
    `MSYS_NO_PATHCONV=1` when a property holds a path on the Mac (such as `CodesignProvision=/Users/...`), or Git Bash
    turns it into a Windows path.
  - ⚠️ **Tested .NET for iOS versions only.** The watch app starts .NET for iOS through internal functions (see
    "watchOS runtime" under Design decisions), so the SDK fails the build with `WATCHIOUS003` on a .NET for iOS
    version it hasn't been tested with. Tested: `27.2.10000` (.NET 10, workload set `10.0.404`:
    `dotnet workload update --version 10.0.404`; also on a real watch) and `27.2.11039` (.NET 11.0.100, in the
    watch simulator). `-p:WatchiousAllowUntestedIOSSdk=true` builds anyway, with a warning. A canary CI job
    (`.github/workflows/watchos-canary.yml`) builds and starts the watch sample on each new .NET for iOS version
    before it is added to the list (`WatchiousTestedIOSSdkVersions`). Its badge at the top turns red when a newer
    .NET release, the next preview included, breaks the start-up, even while the tested versions still work; the run
    shows which version it was.
- **Release:** from CI pipelines, like a MAUI app: `dotnet publish -f net11.0-android` gives the Play Store
  bundle, and `dotnet build -f net11.0-watchos -r ios-arm64 -p:BuildIpa=true` on a macOS runner gives the
  App Store `.ipa`, inside the watch-only container Apple requires (`WatchiousContainerProvision` is the
  container's profile). Signing comes from the pipeline's secrets. A Wear OS release build runs R8, which drops the
  Java code the app never calls (the sample's code shrinks from 52 MB to 13 MB); an app that sets `AndroidLinkTool`
  itself keeps its own choice.
- The repository itself still builds with .NET 10 (`net10.0-*`) until .NET 11 is installed on its
  development machines. The Wear OS libraries come from Gradle first: `./gradlew assembleRelease` in
  `platforms/WearOS` (with Android Studio's JDK) also writes the POMs and binding jars the .NET projects read.
- **Companion apps:** a watch app can ship inside a .NET iPhone app (for example a MAUI app), which
  WatchConnectivity needs: iOS connects the two only when the watch app came inside the iPhone app (tried on a real
  watch). Until .NET for iOS's `AdditionalWatchOSApp` item (https://github.com/dotnet/macios/pull/27284, still open)
  ships, the iPhone project imports Watchious.Sdk's `build/Watchious.Sdk.Companion.targets` and names the watch
  project: `<WatchiousWatchApp Include="..\MyWatch\MyWatch.csproj" CodesignProvision="<watch app profile>" />`. The
  iPhone build then builds the watch app for the same device or simulator, as its companion
  (`WatchiousCompanionAppBundleIdentifier`, which also works on its own), and puts it into the app's `Watch/` folder
  before signing it; `samples/Watchious.Samples.Phone` does this. It needs a Mac. The watch app's bundle identifier
  must start with the iPhone app's (the same `ApplicationId` in both projects does that), and the watch project is
  built only through the iPhone project, not also in parallel in the same solution build.

| Package (working names) | MAUI equivalent | Contents |
|---|---|---|
| `Watchious.Controls` | `Microsoft.Maui.Controls` | Razor controls, navigation, app builder |
| `Watchious.Core` | `Microsoft.Maui.Core` | Renderer and UI-change protocol |
| `Watchious.Hosts.WearOS`, `Watchious.Hosts.watchOS` | the platform parts of `Microsoft.Maui.Controls` | The native hosts; the SDK adds them |
| `Watchious.Essentials` | `Microsoft.Maui.Essentials` | The common watch APIs as services; the hosts add the platform implementations |
| `Watchious.Messaging` | none (closest: [Plugin.Maui.WearableMessaging](https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging) on the phone) | Phone ↔ watch messaging, wire-compatible with that plugin. Optional. |
| `Watchious.Health` | [`Kebechet.Maui.Health`](https://github.com/Kebechet/Maui.Health) (not part of MAUI itself) | Permissions and live workouts with heart rate (HealthKit / Health Services), with its names and its types (`Kebechet.Types.Health`). Optional. |
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
| Android libraries | The host and the optional packages take AndroidX, Compose, Kotlin and Play Services from Microsoft's NuGet bindings (`Xamarin.AndroidX.*`, `Xamarin.GooglePlayServices.*`), like a MAUI app. NuGet then keeps one copy of each library, shared with any other package the app adds; bundling the jars inside Watchious's own packages made the app's build fail with duplicate classes as soon as a second package brought the same library. The few libraries Microsoft doesn't publish (Wear Compose Material3, Health Services 1.1) come from Google's Maven repository (`AndroidMavenLibrary`), and .NET for Android's Java dependency verification fails the build when a dependency has no package. |
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
| Health and workouts | A separate, optional `Watchious.Health` package |
| Styling | Semantic tokens plus a few explicit values. Never CSS. |
| Navigation | Blazor `@page` routes plus source-generated typed helpers: `Navigation.GoToAsync(Routes.Workout(exerciseId: 5))`. A stack of pages, shown with each platform's own navigation and back gestures. |
| Phone ↔ watch messaging | A separate, optional `Watchious.Messaging` package for the watch, **wire-compatible with [Plugin.Maui.WearableMessaging](https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging)**, so a MAUI phone app uses that plugin unchanged: on Wear OS its `/message` JSON messages, `_requestId`/`_replyToId` replies, `/application_context` data item and `wearable_app` capability; on watchOS plain WatchConnectivity dictionaries with native replies. No Watchious phone package, because the plugin already covers the phone side of both platforms with the same concepts; gaps go upstream. The platforms require that on Wear OS the phone and watch apps share the applicationId and signing key, and that on watchOS the watch app is the iPhone app's companion (https://github.com/Kebechet/Watchious/issues/4). |
| watchOS runtime | NativeAOT, built as .NET for iOS (`ios-arm64`) and relabelled for watchOS. Microsoft's runtime glue (`libxamarin`) is started the way its own `main` starts it, minus the iPhone app. ⚠️ That relies on internal start-up functions of .NET for iOS, which may change between versions, so the SDK builds only with the .NET for iOS versions it was tested with (`WatchiousAllowUntestedIOSSdk` overrides that) and a canary CI job tries each new one. |
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
   hosts together, which `HostCoverageTests` checks.
3. ✅ **Project model:** `Watchious.Sdk`, `dotnet new watchious` and `dotnet build -f net11.0-watchos` on top
   of .NET for iOS, App Store packaging, and remote builds from Windows on a Mac. `dotnet build -t:Run` from
   Windows builds, installs and starts the app on a real Apple Watch SE 3, and `HttpClient` works there: a real
   watch has no sockets (https://developer.apple.com/documentation/technotes/tn3135-low-level-networking-on-watchos),
   but .NET for iOS sends `HttpClient` through URLSession by default. Code that opens its own sockets, such as
   `ClientWebSocket` or `SocketsHttpHandler`, can't work there.
4. ✅ **Patch-based protocol:** after each render, the renderer sends the host only what changed, as one
   binary buffer of patches (create, set attribute or event, set text, insert, remove, move), instead of the
   whole UI tree as JSON (https://github.com/Kebechet/Watchious/issues/2). Both hosts apply them to an
   observable node tree, so SwiftUI and Compose redraw only the changed controls. A counter tap now sends
   29 bytes instead of 569, and adding a row to a 50-row list 243 bytes instead of 13,965. On a Galaxy Watch7,
   50 counter taps grew the app's memory by 8.6 MB instead of 10.9 MB.
5. ✅ **Controls and navigation:** the controls under SwiftUI's names, the rest of the planned controls including
   `List`, and navigation with typed routes. ✅ All controls in the table above, and navigation (`@page` routes,
   `INavigation`, generated `Routes` helpers, native back on both platforms). ✅ System icons for `Image`: a shared set
   as SF Symbols and Material icons, and any other icon of either platform by name.
6. **Watch APIs** (`Watchious.Essentials`): the common watch features as unified C# services, with
   platform C# as the escape hatch. ✅ Haptics, device info, battery, connectivity, preferences, secure storage,
   app lifecycle, always-on (ambient) mode, local notifications, location and sensors. The ones added last (secure
   storage to sensors) work on an Apple Watch SE 3 and on the Wear OS emulator.
7. ✅ **Messaging** between phone and watch (`Watchious.Messaging`, optional,
   https://github.com/Kebechet/Watchious/issues/4). ✅ A Galaxy Watch7 and a Pixel 6 running the plugin exchange
   messages, answers and the application context both ways, and so do an Apple Watch SE 3 and an iPhone SE, with the
   watch app installed from inside the iPhone app (see "Companion apps").
8. ✅ **Preview host:** the same screens on the desktop, for debugging in Visual Studio (`Watchious.Preview`,
   see "Preview and inspector").
9. ✅ **Live inspector:** a local page that shows a running watch app's UI tree, updates and events, and can send
   it events (https://github.com/Kebechet/Watchious/issues/5). Works with a real Galaxy Watch7 and a real Apple Watch
   SE 3. The app's side uses plain HTTP requests, which a real Apple Watch allows (it refused the earlier WebSocket).
10. ✅ **Health and workouts** (`Watchious.Health`, optional). A run with live heart rate from the wrist, pause,
    resume and a summary works on a Galaxy Watch7 and an Apple Watch SE 3, and on the Wear OS emulator with Health
    Services' synthetic data. A workout keeps running when the user leaves the app, and on an Apple Watch SE 3 it
    comes back after the app was killed.
11. **Live mode:** the app's C# runs on the computer, under the debugger, and a host app without .NET draws it
    in the watchOS simulator or the Wear OS emulator, connected like the inspector. Unlike the preview, the
    screens are the real SwiftUI and Compose controls. From Windows, the simulator on the Mac connects to the
    computer over the network. Not on a real Apple Watch, which allows no such connection. The idea comes from
    SwiftDotNet's live mode.
12. ✅ **Crown input for values:** the Digital Crown and the Wear OS rotary input change a value, such as a weight
    or a number of reps, not only scroll a list or turn a `Picker`: `DigitalCrownRotation`, on SwiftUI's
    `digitalCrownRotation` and Compose's `onRotaryScrollEvent`, one crown step per value step, clockwise up. Works on
    an Apple Watch SE 3 and on the Wear OS emulator.

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
- [SwiftDotNet](https://github.com/shinyorg/SwiftDotNet): SwiftUI-style views written as C# classes (no
  Razor), shown as native UI on many platforms, among them SwiftUI on Apple Watch and Compose on Wear OS. On
  watchOS it also compiles the C# with NativeAOT for iOS and re-stamps it for watchOS. Its live mode runs the
  C# on the Mac, under a debugger, while a shell app in the watch simulator draws it.
- [Codename One](https://www.codenameone.com/blog/native-apple-watch-and-wear/): Java apps for Apple Watch
  and Wear OS from one code base. On watchOS it draws with Core Graphics inside a SwiftUI shell instead of
  using SwiftUI controls.
- [Uno Platform 6.7](https://platform.uno/blog/uno-platform-6-7/): Wear OS as an opt-in for an Android
  project (AndroidX Wear libraries and manifest entries). The watch UI is still yours to build, and there
  is no watchOS.

## License

[MIT](LICENSE)
