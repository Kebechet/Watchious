# Kebechet.Watchious.Controls

The controls of [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: proof of concept.** The API can still change from one version to the next.

## What it is

The Razor controls with SwiftUI's names (`VStack`, `HStack`, `ScrollView`, `List`, `Label`, `Button`, `Toggle`,
`Image`, `Picker`, `TextField`...), the platform-only controls in the `Apple` and `Wear` namespaces, page navigation,
and the app builder (`WatchiousApp.CreateBuilder()`, with dependency injection, as in MAUI). A source generator in the
package turns each `@page` route into a typed method on `Routes`, so navigation is checked by the compiler.

```razor
<ScrollView>
    <VStack Spacing="8">
        <Label Style="TextStyle.Title">Hello, watch!</Label>
        <Label>Count: @count</Label>
        <Button Text="+1" OnClick="() => count++" />
    </VStack>
</ScrollView>

@code {
    private int count;
}
```

## When an app needs it

Always, and `Kebechet.Watchious.Sdk` adds it to every app at the SDK's version, so an app doesn't reference it itself.
Start from the template, whose first page is the one above:

```bash
dotnet new install Kebechet.Watchious.Templates
dotnet new watchious -n MyWatchApp
```

## More

Every control, with what it is on each platform: "Controls" in the
[Watchious README](https://github.com/Kebechet/Watchious#controls).
