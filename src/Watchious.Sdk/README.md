# Kebechet.Watchious.Sdk

The MSBuild SDK of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: proof of concept.** The API can still change from one version to the next.

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

</Project>
```

`<version>` is the version of this package. The target frameworks are:

- `net10.0-android`: the Wear OS app, built with .NET for Android (the `android` workload);
- `net10.0-watchos`: the watchOS app. .NET has no watchOS target, so the SDK builds it with .NET for iOS (the `ios`
  workload) and NativeAOT, and links it into a SwiftUI host. It needs Xcode on a Mac;
- `net10.0`: the desktop preview, which runs the pages on the computer under the debugger.

The SDK adds the Watchious packages of its own version itself: `Kebechet.Watchious.Controls`, the host of the watch
(`Kebechet.Watchious.Hosts.WearOS` or `Kebechet.Watchious.Hosts.watchOS`) and `Kebechet.Watchious.Preview` for the
preview. The optional packages, `Kebechet.Watchious.Messaging` and `Kebechet.Watchious.Health`, an app adds itself, at
the SDK's version, `$(WatchiousVersion)`:

```xml
<ItemGroup>
  <PackageReference Include="Kebechet.Watchious.Health" Version="$(WatchiousVersion)" />
</ItemGroup>
```

## WATCHIOUS003: an untested .NET for iOS version

The watchOS app starts .NET for iOS through its internal functions, which any .NET for iOS release can change. So a
`net10.0-watchos` build fails with `WATCHIOUS003` when the installed .NET for iOS is a version this SDK hasn't been
tested with; the error names the tested ones. Install a tested version, for .NET 10 the workload set `10.0.404`:

```bash
dotnet workload update --version 10.0.404
```

or build anyway, with the warning `WATCHIOUS003` instead of the error, knowing that the app can crash at start-up on the
watch:

```bash
dotnet build -f net10.0-watchos -p:WatchiousAllowUntestedIOSSdk=true
```

(`<WatchiousAllowUntestedIOSSdk>true</WatchiousAllowUntestedIOSSdk>` in the project does the same.)

## More

Running on a watch, a simulator or an emulator, building on a remote Mac from Windows, App Store packaging and iPhone
companion apps: "Project and packaging" in the
[Watchious README](https://github.com/Kebechet/Watchious#project-and-packaging).
