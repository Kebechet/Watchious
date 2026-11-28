# Demo apps

Two watch apps made the way a user makes one: `dotnet new watchious`, built from the `Kebechet.Watchious.*` packages on
nuget.org, with nothing from this repository's sources. Copy one out of the repository and it builds as it is.

| App | What it is | What it shows |
|---|---|---|
| [DotNetSplash](DotNetSplash) | MAUI's splash screen: the .NET bot and wordmark on `#512BD4` | the app's background from `ConfigureTheme`; a page that doesn't scroll, centered; images from `Resources/Images` |
| [Breathing](Breathing) | Guided breathing: pick a pattern (Box, 4-7-8, Calm) and a number of breaths, then breathe with the watch, which taps your wrist at each step while a glow grows as you breathe in and shrinks as you breathe out | a `List`; navigation with route parameters (`Routes.Session(...)`); `Picker` and `Toggle`; `IPreferences` (the choices and the totals stay on the watch); `IHaptics`; `IDeviceDisplay.KeepScreenOn` (a mindfulness session on watchOS); `IAppLifecycle` (a session waits while the app is away); a theme gradient, and a page's own `RadialGradientBrush` that changes up to ten times a second; tests that run the app with fake watch APIs |

[`samples/`](../samples) is the other kind of app: it builds from the repository's sources, to work on Watchious
itself, and shows every control and watch API.

## Run

```bash
cd demo/Breathing
dotnet run -f net10.0                     # the preview, in a browser
dotnet build -t:Run -f net10.0-android    # Wear OS: the emulator or a watch
dotnet build -t:Run -f net10.0-watchos    # watchOS: the simulator, on a Mac
```

On Windows, a Mac builds the watchOS app: `-p:WatchiousRemoteMac=user@host` (README, "Project and packaging").

`Breathing.Tests` runs Breathing's pages on .NET with fake watch APIs, the way a watch's host starts the app
(`WatchiousPlatform.ConfigureServices`, then `WatchiousApp.StartAsync` with a host that takes the render batches):
`dotnet test --project demo/Breathing.Tests/Breathing.Tests.csproj`.

## What each demo changed

The first commit of `demo/` is the template's output as it is; the commits after it are what each demo adds. Besides
its pages, each project sets `ImplicitUsings` and `Nullable`, which the 0.2.0-preview.1 template leaves out (its next
version sets them).

`demo/Directory.Build.props` and `Directory.Build.targets` are empty so that the repository's own build settings, which
build apps from its sources, don't reach the demos. A copy outside the repository doesn't need them.

⚠️ **Each project names the SDK's version**, `<Project Sdk="Kebechet.Watchious.Sdk/0.2.0-preview.1">`. After a release,
a follow-up commit moves both demos to the new version; `publish.yml` can't, because it runs before the packages exist.

Known in 0.2.0-preview.1, fixed for the next version:
- The watchOS build warns once per page that the `.razor` file "would result in a file outside of the app bundle".
  Harmless: the pages aren't copied into the app.
- On watchOS, a `Picker` in a stack is squeezed into a thin bar; in a `List`, as Breathing's, it is a row that opens
  its choices.

## Images

`DotNetSplash/Resources/Images` has the .NET bot and the .NET wordmark from .NET MAUI's own templates (`dotnet_bot.svg`
of `maui-blazor` and `splash.svg` of `maui-mobile`, https://github.com/dotnet/maui), drawn as PNG. Their MIT license
is in `DotNetSplash/THIRD-PARTY-NOTICES.txt`.
