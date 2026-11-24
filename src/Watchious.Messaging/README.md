# Kebechet.Watchious.Messaging

Phone messaging for [Watchious](https://github.com/Kebechet/Watchious), a .NET MAUI alternative for Wear OS and watchOS,
using the Blazor rendering engine. A watch app is written once in C# and Razor; Watchious renders it as native SwiftUI
on Apple Watch and native Compose on Wear OS, and the app's C# runs on the watch itself.

⚠️ **Status: proof of concept.** The API can still change from one version to the next.

## What it is

Messages, messages with an answer, and the application context between a watch app and its phone app. It is
wire-compatible with
[Plugin.Maui.WearableMessaging](https://github.com/Clinical-Support-Systems/Plugin.Maui.WearableMessaging), which the
phone app uses unchanged. On Wear OS it uses the Wearable Data Layer (Play Services), and the phone and watch apps must
share the applicationId and signing key. On watchOS it uses WatchConnectivity, and the watch app must be the iPhone
app's companion, installed from inside it.

## When an app needs it

When the watch app talks to a phone app. The SDK doesn't add it: the app references it at the SDK's version,
`$(WatchiousVersion)`, and registers it:

```xml
<ItemGroup>
  <PackageReference Include="Kebechet.Watchious.Messaging" Version="$(WatchiousVersion)" />
</ItemGroup>
```

```csharp
using Watchious.Messaging;

builder.Services.AddPhoneMessaging();
```

Then a page or service injects `IPhoneMessaging`. On watchOS, the SDK compiles the package's Swift
(`WatchConnectivity.swift`) into the app's SwiftUI host.

## More

"Phone messaging and health" in the
[Watchious README](https://github.com/Kebechet/Watchious#phone-messaging-and-health).
