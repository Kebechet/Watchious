#!/usr/bin/env bash
# Turns the app bundle .NET for iOS created into a watch app: watchOS Info.plist, watch app icon, signature.
# Called by Watchious.Sdk.watchOS.targets, which passes:
#   WATCHIOUS_APP           the .app bundle
#   WATCHIOUS_SDK_NAME      watchos or watchsimulator
#   WATCHIOUS_MIN_OS        minimum watchOS version
#   WATCHIOUS_BUNDLE_ID     the watch app's bundle identifier
#   WATCHIOUS_INFO_PLIST    the app's own Platforms/watchOS/Info.plist, if any (its keys win)
#   WATCHIOUS_ASSETS        the app's Platforms/watchOS/Assets.xcassets, if any (needs an AppIcon set)
#   WATCHIOUS_WORK_DIR      intermediate directory
#   WATCHIOUS_SIGN_IDENTITY signing identity (name or SHA-1); empty signs ad hoc, which only the simulator accepts
#   WATCHIOUS_PROFILE       provisioning profile (.mobileprovision path) for a real watch
#   WATCHIOUS_ENTITLEMENTS  the app's entitlements plist, if any (on the simulator they are in the executable)
#   WATCHIOUS_COMPANION_ID  the bundle identifier of the iPhone app whose companion this watch app is, if any
#   WATCHIOUS_CONFIGURATION the build configuration (Debug lets the app reach the inspector over plain HTTP)
set -euo pipefail

APP="${WATCHIOUS_APP%/}"
PLIST="$APP/Info.plist"
WORK="$WATCHIOUS_WORK_DIR"
mkdir -p "$WORK"

if [ "$WATCHIOUS_SDK_NAME" = "watchsimulator" ]; then
    SUPPORTED_PLATFORM="WatchSimulator"
else
    SUPPORTED_PLATFORM="WatchOS"
fi

# Start from the app's own Info.plist, then add what .NET for iOS generated without overwriting the app's keys. An
# incremental build can leave the bundle's Info.plist as this script wrote it (not newer than the stamp); then .NET for
# iOS's own from an earlier build is used, so that a key removed from the app's Info.plist doesn't stay.
GENERATED="$WORK/Generated-Info.plist"
STAMP="$WORK/Info.plist.stamp"
if [ ! -f "$GENERATED" ] || [ ! -f "$STAMP" ] || [ "$PLIST" -nt "$STAMP" ]; then
    cp "$PLIST" "$GENERATED"
fi
if [ -n "${WATCHIOUS_INFO_PLIST:-}" ] && [ -f "$WATCHIOUS_INFO_PLIST" ]; then
    cp "$WATCHIOUS_INFO_PLIST" "$PLIST"
    plutil -convert xml1 "$PLIST"
    # The path is quoted inside PlistBuddy's command, which splits at spaces.
    /usr/libexec/PlistBuddy -c "Merge '$GENERATED'" "$PLIST" > /dev/null
fi

# Keys that only make sense for an iPhone app.
for key in LSRequiresIPhoneOS UIRequiredDeviceCapabilities UISupportedInterfaceOrientations \
    "UISupportedInterfaceOrientations~ipad" UILaunchScreen UILaunchStoryboardName UIApplicationSceneManifest \
    "CFBundleIcons~ipad"; do
    plutil -remove "$key" "$PLIST" 2>/dev/null || true
done

# The build metadata Xcode writes into every Info.plist it produces; the App Store checks it.
sdk_version="$(xcrun --sdk "$WATCHIOUS_SDK_NAME" --show-sdk-version)"
sdk_build="$(xcrun --sdk "$WATCHIOUS_SDK_NAME" --show-sdk-build-version)"
xcode_version="$(xcodebuild -version | sed -n 's/^Xcode //p')"
xcode_build="$(xcodebuild -version | sed -n 's/^Build version //p')"
IFS=. read -r xcode_major xcode_minor xcode_patch <<< "$xcode_version"
plutil -replace CFBundleIdentifier -string "$WATCHIOUS_BUNDLE_ID" "$PLIST"
plutil -replace MinimumOSVersion -string "$WATCHIOUS_MIN_OS" "$PLIST"
plutil -replace UIDeviceFamily -json '[4]' "$PLIST"
plutil -replace WKApplication -bool YES "$PLIST"
if [ -n "${WATCHIOUS_COMPANION_ID:-}" ]; then
    # WatchConnectivity connects a watch app only with the iPhone app it names; it still runs without it.
    plutil -remove WKWatchOnly "$PLIST" 2>/dev/null || true
    plutil -replace WKCompanionAppBundleIdentifier -string "$WATCHIOUS_COMPANION_ID" "$PLIST"
    plutil -replace WKRunsIndependentlyOfCompanionApp -bool YES "$PLIST"
else
    plutil -replace WKWatchOnly -bool YES "$PLIST"
fi
plutil -replace CFBundleSupportedPlatforms -json "[\"$SUPPORTED_PLATFORM\"]" "$PLIST"
plutil -replace DTPlatformName -string "$WATCHIOUS_SDK_NAME" "$PLIST"
plutil -replace DTPlatformVersion -string "$sdk_version" "$PLIST"
plutil -replace DTPlatformBuild -string "$sdk_build" "$PLIST"
plutil -replace DTSDKName -string "$WATCHIOUS_SDK_NAME$sdk_version" "$PLIST"
plutil -replace DTSDKBuild -string "$sdk_build" "$PLIST"
plutil -replace DTXcode -string "$xcode_major${xcode_minor:-0}${xcode_patch:-0}" "$PLIST"
plutil -replace DTXcodeBuild -string "$xcode_build" "$PLIST"
plutil -replace DTCompiler -string "com.apple.compilers.llvm.clang.1_0" "$PLIST"
plutil -replace BuildMachineOSBuild -string "$(sw_vers -buildVersion)" "$PLIST"

if [ -n "${WATCHIOUS_ASSETS:-}" ] && [ -d "$WATCHIOUS_ASSETS" ]; then
    xcrun actool "$WATCHIOUS_ASSETS" --compile "$APP" --platform "$WATCHIOUS_SDK_NAME" --target-device watch \
        --minimum-deployment-target "$WATCHIOUS_MIN_OS" --app-icon AppIcon --output-partial-info-plist "$WORK/icon.plist" \
        --output-format human-readable-text --errors --warnings > "$WORK/actool.log" || { cat "$WORK/actool.log"; exit 1; }
    /usr/libexec/PlistBuddy -c "Merge '$WORK/icon.plist'" "$PLIST" > /dev/null
fi
# A Debug build mirrors itself to the Watchious inspector on the developer's computer, over plain HTTP on the local
# network, which App Transport Security otherwise refuses. An app that already allows arbitrary loads needs nothing more,
# and NSAllowsLocalNetworking would switch that off: ATS ignores NSAllowsArbitraryLoads when it is present.
if [ "$(printf '%s' "${WATCHIOUS_CONFIGURATION:-}" | tr '[:upper:]' '[:lower:]')" = "debug" ]; then
    if [ "$(plutil -extract NSAppTransportSecurity.NSAllowsArbitraryLoads raw "$PLIST" 2>/dev/null || true)" != "true" ]; then
        plutil -insert NSAppTransportSecurity -json '{}' "$PLIST" 2>/dev/null || true
        plutil -replace NSAppTransportSecurity.NSAllowsLocalNetworking -bool YES "$PLIST"
    fi
    plutil -insert NSLocalNetworkUsageDescription -string "Debug builds mirror the app to the Watchious inspector on your computer." "$PLIST" 2>/dev/null || true
fi
plutil -convert binary1 "$PLIST"
touch "$STAMP"

if [ -z "${WATCHIOUS_PROFILE:-}" ]; then
    if [ "$WATCHIOUS_SDK_NAME" != "watchsimulator" ]; then
        echo "A real watch needs a signing identity and provisioning profile: set CodesignKey and CodesignProvision." >&2
        exit 2
    fi
    codesign --force --sign - --timestamp=none "$APP"
    exit 0
fi

# Signs with only the entitlements the app needs, with the values its profile grants: a development profile allows
# debugging (get-task-allow), an App Store profile marks the build for TestFlight (beta-reports-active). The profile
# also allows a wildcard keychain group, which the app must not claim unless it shares keychain items.
cp "$WATCHIOUS_PROFILE" "$APP/embedded.mobileprovision"
security cms -D -i "$WATCHIOUS_PROFILE" > "$WORK/profile.plist"
application_identifier="$(plutil -extract Entitlements.application-identifier raw "$WORK/profile.plist")"
team_identifier="$(plutil -extract TeamIdentifier.0 raw "$WORK/profile.plist")"
get_task_allow="$(plutil -extract Entitlements.get-task-allow raw "$WORK/profile.plist")"
beta_reports_entry=""
if [ "$(plutil -extract Entitlements.beta-reports-active raw "$WORK/profile.plist" 2>/dev/null || true)" = "true" ]; then
    beta_reports_entry=$'    <key>beta-reports-active</key>\n    <true/>'
fi
cat > "$WORK/app.entitlements" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>application-identifier</key>
    <string>$application_identifier</string>
$beta_reports_entry
    <key>com.apple.developer.team-identifier</key>
    <string>$team_identifier</string>
    <key>get-task-allow</key>
    <$get_task_allow/>
</dict>
</plist>
EOF
# The app's own entitlements (HealthKit, app groups...). Merge keeps the keys above, which the profile decides.
if [ -n "${WATCHIOUS_ENTITLEMENTS:-}" ]; then
    /usr/libexec/PlistBuddy -c "Merge '$WATCHIOUS_ENTITLEMENTS'" "$WORK/app.entitlements" > /dev/null
fi
codesign --force --sign "${WATCHIOUS_SIGN_IDENTITY:?CodesignKey is required with CodesignProvision}" \
    --entitlements "$WORK/app.entitlements" --generate-entitlement-der "$APP"
