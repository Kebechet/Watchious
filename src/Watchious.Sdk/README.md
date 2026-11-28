# Kebechet.Watchious.Sdk

The MSBuild SDK of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: preview.** The API can still change from one version to the next.

## What it does

A Watchious app is one project for both watches, like a MAUI app, and names this SDK with its version. The template
writes it for you:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

```xml
<Project Sdk="Kebechet.Watchious.Sdk/<version>">

  <PropertyGroup>
    <TargetFrameworks>net10.0-android</TargetFrameworks>
    <!-- A watchOS build needs a Mac: this one, or a remote one through WatchiousRemoteMac=user@host. -->
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('OSX')) Or '$(WatchiousRemoteMac)' != ''">$(TargetFrameworks);net10.0-watchos</TargetFrameworks>
    <!-- The desktop preview, for debugging the pages: dotnet run -f net10.0. -->
    <TargetFrameworks>$(TargetFrameworks);net10.0</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <RootNamespace>MyWatchApp</RootNamespace>
    <ApplicationId>com.companyname.mywatchapp</ApplicationId>
    <ApplicationTitle>MyWatchApp</ApplicationTitle>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>
  </PropertyGroup>

  <ItemGroup>
    <WatchiousAppIcon Include="Resources/AppIcon/appicon.svg" BackgroundColor="#17122B" />
  </ItemGroup>

</Project>
```

`<version>` is the version of this package: the template writes its own, the one all Watchious packages share, and
`dotnet new watchious --sdk-version <version>` writes another. To update Watchious in an app, change this one number;
the SDK adds the other packages at its own version. ⚠️ With Central Package Management, change the
`Kebechet.Watchious.*` entries in `Directory.Packages.props` with it (see below). The target frameworks are:

- `net10.0-android`: the Wear OS app, built with .NET for Android (the `android` workload);
- `net10.0-watchos`: the watchOS app. .NET has no watchOS target, so the SDK builds it with .NET for iOS (the `ios`
  workload) and NativeAOT, and links it into a SwiftUI host. It needs Xcode on a Mac;
- `net10.0`: the desktop preview, which runs the pages on the computer, where a .NET debugger works.

The SDK adds the Watchious packages of its own version itself: `Kebechet.Watchious.Controls`, the host of the watch
(`Kebechet.Watchious.Hosts.WearOS` or `Kebechet.Watchious.Hosts.watchOS`) and `Kebechet.Watchious.Preview` for the
preview. The optional packages, `Kebechet.Watchious.Messaging` and `Kebechet.Watchious.Health`, an app adds itself, at
the SDK's version, `$(WatchiousVersion)`:

```xml
<ItemGroup>
  <PackageReference Include="Kebechet.Watchious.Health" Version="$(WatchiousVersion)" />
</ItemGroup>
```

⚠️ **With Central Package Management** (a `Directory.Packages.props`), leave `Version` off these references and give
each `Kebechet.Watchious.*` package the solution uses a `PackageVersion` there, at this package's version written out:
a library project of the solution has no `$(WatchiousVersion)`. The SDK ignores the entries of the packages it adds
itself. ⚠️ Nothing reports an entry left at an older version, so an update changes the SDK's version and these entries
together.

## App icon

One item draws the app's icon for both watches, from an SVG or a PNG:

```xml
<ItemGroup>
  <WatchiousAppIcon Include="Resources/AppIcon/appicon.svg"
                    ForegroundSize="65%"
                    ForegroundTintColor="#FFFFFF"
                    BackgroundColor="#512BD4"
                    BackgroundFile="Resources/AppIcon/background.svg" />
</ItemGroup>
```

| Setting | What it is | Default |
|---|---|---|
| `Include` | The picture: an SVG, PNG or JPEG, drawn as large as fits into `ForegroundSize`, its own margins included | required |
| `ForegroundSize` | How much of the icon's width the picture takes, from `1%` to `100%`. `100%` for a finished edge-to-edge icon | `65%` |
| `ForegroundTintColor` | Paints every shape of the picture in one color, its transparency kept | the picture's own colors |
| `BackgroundColor` | The color behind the picture: `#RRGGBB`, `#AARRGGBB` or `#RGB` | black |
| `BackgroundFile` | An image behind the picture (SVG, PNG or JPEG), covering the icon, over `BackgroundColor` | none |

Both watches show an app icon cut into a circle, and `ForegroundSize` is a share of the width each of them shows, so
the picture is as large on both. The SDK draws, when the app is built:
- watchOS: an opaque 1024×1024 image in the `AppIcon` set of an asset catalog, from which `actool` makes every size;
- Wear OS: an adaptive icon (`@mipmap/appicon`, its background and foreground layers), which the SDK names in the
  manifest.

A different icon for one watch is an item with a condition:
`Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'watchos'"` (or `'android'`).
Without the item, an app keeps its icon each platform's own way: `Platforms/watchOS/Assets.xcassets` with an
`AppIcon` set, and `android:icon` in `Platforms/WearOS/AndroidManifest.xml`. The build fails when one is there along
with the item (`WATCHIOUS007`), and when a platform has more than one item or the item can't be drawn: a missing or
unsupported file, a size or color it can't read (`WATCHIOUS006`, with the reason).

The icon tool (`appicon/` in this package) runs with the `dotnet` that builds the app and draws with SkiaSharp and
Svg.Skia, as MAUI's own icon tool does. It carries their native libraries for Windows, macOS and Linux (x64 and Arm64).

## WATCHIOUS003: an untested .NET for iOS version

The watchOS app starts .NET for iOS through its internal functions, which any .NET for iOS release can change. So a
`net10.0-watchos` build fails with `WATCHIOUS003` when the installed .NET for iOS is a version this SDK hasn't been
tested with; the error names the tested ones. Install a tested version, for .NET 10 the workload set `10.0.404`:

```bash
dotnet workload update --version 10.0.404
```

Run it on the Mac that builds the watchOS app. From Windows or Linux with `WatchiousRemoteMac`, that is the remote Mac,
with the dotnet the remote build uses (`WatchiousRemoteDotnet`, by default `~/.dotnet/dotnet`,
`/usr/local/share/dotnet/dotnet` or the one on the PATH, the first there is):
`ssh user@host '~/.dotnet/dotnet workload update --version 10.0.404'`.

⚠️ **Building anyway can crash the app at start-up on the watch.** To build anyway, with the warning `WATCHIOUS003`
instead of the error:

```bash
dotnet build -f net10.0-watchos -p:WatchiousAllowUntestedIOSSdk=true
```

(`<WatchiousAllowUntestedIOSSdk>true</WatchiousAllowUntestedIOSSdk>` in the project does the same.)

## Live mode

`dotnet run -f net10.0 -- --live` runs the app's C# on the computer, where a .NET debugger works (to debug it, start
the `net10.0` framework from the IDE with the argument `--live`), and a live host, a watch app without .NET that this
package ships, draws it with the real Compose or SwiftUI controls. In another terminal, start the
live host:

```bash
dotnet build -t:WatchiousLive -tl:off -f net10.0-android   # the Wear OS emulator or a watch connected to adb
dotnet build -t:WatchiousLive -tl:off -f net10.0-watchos   # the watchOS simulator, on a Mac or a remote Mac
```

⚠️ **Keep `-tl:off`:** since .NET 9, `dotnet build` in a terminal hides a build's messages, and these say where the live
host runs, how to start the app and, from Windows, that the ssh tunnel to the remote Mac stays open until Ctrl+C.
Without them the tunnel looks like a build that hangs. ⚠️ **While the tunnel is open, every account on that Mac can
start the app and tap in it** through the Mac's port: use a Mac no one else uses.

An Apple Watch gets the live host signed as the app itself, which it replaces until the app is run again:
`-r ios-arm64 -p:WatchiousDevice=<watch> -p:WatchiousLiveUrl=http://<computer>:5293`, with the app started with
`--urls=http://0.0.0.0:5293`. ⚠️ **That address lets every device on the network start the app and tap in it:** use
it on a network you trust only. ⚠️ **Keep the watch's screen on:** watchOS suspends an app whose screen is off, the
live host too. ⚠️ **Open it on the watch:** watchOS doesn't bring an app started from the Mac to the front, so tap
Watchious Live, which has a default icon and stands in for the app, if the watch stays on its app grid.

Tried in the Wear OS emulator and the watchOS simulator (from a Mac, and from Windows through a remote Mac), on a Galaxy
Watch7 over adb, and on an Apple Watch SE 3 with the app on Windows.

## License

Watchious is MIT-licensed. The Wear OS live host in this package (`live/WearOS/watchious-live.apk`) is built with
AndroidX, Kotlin and other libraries under the Apache License 2.0, and the icon tool (`appicon/`) carries SkiaSharp
(with Skia, BSD-3-Clause), HarfBuzzSharp and Svg.Skia (MIT; its Svg.Custom under the MS-PL): `THIRD-PARTY-NOTICES.txt`
in the package lists them, with their licenses.

## More

Running on a watch, a simulator or an emulator, building on a remote Mac from Windows, App Store packaging and iPhone
companion apps: "Project and packaging" in the
[Watchious README](https://github.com/Kebechet/Watchious#project-and-packaging).
