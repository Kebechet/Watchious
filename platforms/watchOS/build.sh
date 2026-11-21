#!/usr/bin/env bash
# Builds the Counter sample for watchOS: NativeAOT static library -> re-tagged for watchOS -> linked into the
# SwiftUI host -> .app bundle.
#   (default)  For the watchOS simulator. Pass --install to (re)install and launch it on a simulator.
#   --device   For real watches, signed with WATCHIOUS_CODESIGN_IDENTITY (in an unlocked keychain) and the
#              provisioning profile WATCHIOUS_WATCH_PROFILE:
#              - a development profile: pass --install to install and launch it on the watch WATCHIOUS_DEVICE
#                (its UDID, from `xcrun devicectl list devices`);
#              - an App Store profile: also set WATCHIOUS_CONTAINER_PROFILE to wrap it in a watch-only container,
#                as an .ipa for the App Store.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
APP_NAME="Watchious"
EXECUTABLE="WatchiousHost"
DEVICE_NAME="Watchious Watch"

IS_DEVICE=false
INSTALL=false
LAUNCH_ARGUMENTS=()
for argument in "$@"; do
    case "$argument" in
        --device) IS_DEVICE=true ;;
        --install) INSTALL=true ;;
        *) LAUNCH_ARGUMENTS+=("$argument") ;;
    esac
done

if [ "$IS_DEVICE" = true ]; then
    # Without an arm64_32 slice, which NativeAOT cannot build, the App Store accepts a watch app only from
    # watchOS 27 (ITMS-90733).
    MIN_WATCHOS="${WATCHIOUS_MIN_WATCHOS:-27.0}"
    RID="ios-arm64"
    SDK_NAME="watchos"
    TARGET_TRIPLE="arm64-apple-watchos${MIN_WATCHOS}"
    SUPPORTED_PLATFORM="WatchOS"
else
    # Kept at 26.0 so watchOS 26 simulators still run it.
    MIN_WATCHOS="${WATCHIOUS_MIN_WATCHOS:-26.0}"
    RID="iossimulator-arm64"
    SDK_NAME="watchsimulator"
    TARGET_TRIPLE="arm64-apple-watchos${MIN_WATCHOS}-simulator"
    SUPPORTED_PLATFORM="WatchSimulator"
fi
BUILD="${WATCHIOUS_OUT:-$HOME/watchious/out}/$SDK_NAME"
BUNDLE_ID="$(plutil -extract CFBundleIdentifier raw "$HERE/Host/Info.plist")"

export DOTNET_ROOT="$(dirname "$DOTNET")" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
PROJECT="$HERE/../../samples/Watchious.Samples.Counter.watchOS/Watchious.Samples.Counter.watchOS.csproj"
SDK_VERSION="$(xcrun --sdk "$SDK_NAME" --show-sdk-version)"

# The build metadata Xcode writes into every Info.plist it produces; the App Store checks it.
stamp_info_plist() {
    local plist="$1" sdk="$2" platform="$3"
    local sdk_version sdk_build xcode_version xcode_build xcode_major xcode_minor xcode_patch
    sdk_version="$(xcrun --sdk "$sdk" --show-sdk-version)"
    sdk_build="$(xcrun --sdk "$sdk" --show-sdk-build-version)"
    xcode_version="$(xcodebuild -version | sed -n 's/^Xcode //p')"
    xcode_build="$(xcodebuild -version | sed -n 's/^Build version //p')"
    IFS=. read -r xcode_major xcode_minor xcode_patch <<< "$xcode_version"
    plutil -replace CFBundleSupportedPlatforms -json "[\"$platform\"]" "$plist"
    plutil -replace DTPlatformName -string "$sdk" "$plist"
    plutil -replace DTPlatformVersion -string "$sdk_version" "$plist"
    plutil -replace DTPlatformBuild -string "$sdk_build" "$plist"
    plutil -replace DTSDKName -string "$sdk$sdk_version" "$plist"
    plutil -replace DTSDKBuild -string "$sdk_build" "$plist"
    plutil -replace DTXcode -string "$xcode_major${xcode_minor:-0}${xcode_patch:-0}" "$plist"
    plutil -replace DTXcodeBuild -string "$xcode_build" "$plist"
    plutil -replace DTCompiler -string "com.apple.compilers.llvm.clang.1_0" "$plist"
    plutil -replace BuildMachineOSBuild -string "$(sw_vers -buildVersion)" "$plist"
}

compile_icon() {
    local catalog="$1" bundle="$2" platform="$3" device="$4" minimum_os="$5" icon="$6"
    local partial_plist="$BUILD/$(basename "$bundle")-icon.plist"
    xcrun actool "$catalog" --compile "$bundle" --platform "$platform" --target-device "$device" \
        --minimum-deployment-target "$minimum_os" --app-icon "$icon" --output-partial-info-plist "$partial_plist" \
        --output-format human-readable-text --errors --warnings > "$BUILD/actool.log" \
        || { cat "$BUILD/actool.log"; exit 1; }
    /usr/libexec/PlistBuddy -c "Merge $partial_plist" "$bundle/Info.plist" > /dev/null
}

# Signs with only the entitlements the app needs, with the values its profile grants: a development profile allows
# debugging (get-task-allow), an App Store profile marks the build for TestFlight (beta-reports-active). The profile
# also allows a wildcard keychain group, which the app must not claim unless it shares keychain items.
sign_with_profile() {
    local bundle="$1" profile="$2"
    local profile_plist="$BUILD/$(basename "$bundle")-profile.plist"
    local entitlements="$BUILD/$(basename "$bundle").entitlements"
    local application_identifier team_identifier get_task_allow beta_reports_entry=""
    cp "$profile" "$bundle/embedded.mobileprovision"
    security cms -D -i "$profile" > "$profile_plist"
    application_identifier="$(plutil -extract Entitlements.application-identifier raw "$profile_plist")"
    team_identifier="$(plutil -extract TeamIdentifier.0 raw "$profile_plist")"
    get_task_allow="$(plutil -extract Entitlements.get-task-allow raw "$profile_plist")"
    if [ "$(plutil -extract Entitlements.beta-reports-active raw "$profile_plist" 2>/dev/null || true)" = "true" ]; then
        beta_reports_entry=$'    <key>beta-reports-active</key>\n    <true/>'
    fi
    cat > "$entitlements" <<EOF
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
    codesign --force --sign "$WATCHIOUS_CODESIGN_IDENTITY" --entitlements "$entitlements" --generate-entitlement-der \
        "$bundle"
}

echo "==> NativeAOT static library ($RID)"
"$DOTNET" publish "$PROJECT" -c "$CONFIGURATION" -r "$RID" -o "$BUILD/nativeaot" -nologo -v:q
RUNTIME_VERSION="$("$DOTNET" msbuild "$PROJECT" -getProperty:BundledNETCoreAppPackageVersion)"
NATIVE_DIR="$HOME/.nuget/packages/microsoft.netcore.app.runtime.nativeaot.$RID/$RUNTIME_VERSION/runtimes/$RID/native"

NATIVE_INPUTS=(
    "$BUILD/nativeaot/WatchiousCounter.a"
    "$NATIVE_DIR/libbootstrapperdll.o"
    "$NATIVE_DIR/libRuntime.WorkstationGC.a"
    "$NATIVE_DIR/libeventpipe-disabled.a"
    "$NATIVE_DIR/libstandalonegc-disabled.a"
    "$NATIVE_DIR/libaotminipal.a"
    "$NATIVE_DIR/libstdc++compat.a"
    "$NATIVE_DIR/libSystem.Native.a"
    "$NATIVE_DIR/libSystem.IO.Compression.Native.a"
    "$NATIVE_DIR/libSystem.Security.Cryptography.Native.Apple.a"
    "$NATIVE_DIR/libbrotlienc.a"
    "$NATIVE_DIR/libbrotlidec.a"
    "$NATIVE_DIR/libbrotlicommon.a"
)

echo "==> Re-tag objects for $SDK_NAME (min $MIN_WATCHOS, sdk $SDK_VERSION)"
if [ "$IS_DEVICE" = true ]; then RETAG_TARGET="watchos"; else RETAG_TARGET="watchossimulator"; fi
python3 "$HERE/build/retag-macho.py" --target "$RETAG_TARGET" --minos "$MIN_WATCHOS" --sdk "$SDK_VERSION" \
    --output-dir "$BUILD/watchos-libs" "${NATIVE_INPUTS[@]}"
RETAGGED=()
for input in "${NATIVE_INPUTS[@]}"; do
    RETAGGED+=("$BUILD/watchos-libs/$(basename "$input")")
done

echo "==> Link SwiftUI host"
APP="$BUILD/$APP_NAME.app"
mkdir -p "$APP"
xcrun --sdk "$SDK_NAME" clang -target "$TARGET_TRIPLE" -O2 -c "$HERE/Host/FatalSignals.c" -o "$BUILD/FatalSignals.o"
xcrun --sdk "$SDK_NAME" swiftc \
    -target "$TARGET_TRIPLE" \
    -parse-as-library \
    -O \
    -import-objc-header "$HERE/Host/Watchious.h" \
    "$HERE"/Host/*.swift \
    "$BUILD/FatalSignals.o" \
    "${RETAGGED[@]}" \
    -framework CoreFoundation -framework CryptoKit -framework Foundation -framework Network -framework Security \
    -lobjc -lswiftCore -lswiftFoundation -licucore -lz -lm \
    -Xlinker -dead_strip \
    -o "$APP/$EXECUTABLE"
cp "$HERE/Host/Info.plist" "$APP/Info.plist"
plutil -replace MinimumOSVersion -string "$MIN_WATCHOS" "$APP/Info.plist"
stamp_info_plist "$APP/Info.plist" "$SDK_NAME" "$SUPPORTED_PLATFORM"
compile_icon "$HERE/Host/Assets.xcassets" "$APP" "$SDK_NAME" watch "$MIN_WATCHOS" AppIcon
plutil -convert binary1 "$APP/Info.plist"

if [ "$IS_DEVICE" = true ]; then
    echo "==> Sign"
    sign_with_profile "$APP" "$WATCHIOUS_WATCH_PROFILE"
    echo "==> Sizes"
    du -sh "$BUILD/nativeaot/WatchiousCounter.a" "$APP/$EXECUTABLE"

    if [ -n "${WATCHIOUS_CONTAINER_PROFILE:-}" ]; then
        echo "==> Watch-only container"
        # The App Store takes a watch-only app inside an iPhone stub app, which Xcode builds from Apple's
        # MessagesApplicationStub (product type com.apple.product-type.application.watchapp2-container).
        STUB_DIR="$(xcrun --sdk iphoneos --show-sdk-platform-path)/Library/Application Support/MessagesApplicationStub"
        CONTAINER="$BUILD/Payload/$APP_NAME.app"
        CONTAINER_MIN_IOS="$(plutil -extract MinimumOSVersion raw "$HERE/Host/Container-Info.plist")"
        mkdir -p "$CONTAINER/Watch"
        lipo "$STUB_DIR/MessagesApplicationStub" -thin arm64 -output "$CONTAINER/$APP_NAME"
        cp "$HERE/Host/Container-Info.plist" "$CONTAINER/Info.plist"
        stamp_info_plist "$CONTAINER/Info.plist" iphoneos iPhoneOS
        compile_icon "$STUB_DIR/MessagesApplicationStub.xcassets" "$CONTAINER" iphoneos iphone "$CONTAINER_MIN_IOS" \
            MessagesApplicationStub
        plutil -convert binary1 "$CONTAINER/Info.plist"
        ditto "$APP" "$CONTAINER/Watch/$APP_NAME.app"
        sign_with_profile "$CONTAINER" "$WATCHIOUS_CONTAINER_PROFILE"
        ditto -c -k --norsrc --keepParent "$BUILD/Payload" "$BUILD/$APP_NAME.ipa"
        du -sh "$BUILD/$APP_NAME.ipa"
        echo "IPA=$BUILD/$APP_NAME.ipa"
    fi

    if [ "$INSTALL" = true ]; then
        echo "==> Install on watch $WATCHIOUS_DEVICE"
        xcrun devicectl device install app --device "$WATCHIOUS_DEVICE" "$APP"
        xcrun devicectl device process launch --terminate-existing --device "$WATCHIOUS_DEVICE" "$BUNDLE_ID" \
            "${LAUNCH_ARGUMENTS[@]+"${LAUNCH_ARGUMENTS[@]}"}"
    fi
    exit 0
fi

codesign --force --sign - --timestamp=none "$APP"

echo "==> Sizes"
du -sh "$BUILD/nativeaot/WatchiousCounter.a" "$APP" "$APP/$EXECUTABLE"

if [ "$INSTALL" != true ]; then
    exit 0
fi

echo "==> Install on simulator '$DEVICE_NAME'"
RUNTIME_ID="$(xcrun simctl list runtimes | grep -o 'com.apple.CoreSimulator.SimRuntime.watchOS-[0-9-]*' | tail -1)"
DEVICE_ID="$(xcrun simctl list devices | grep -F "$DEVICE_NAME (" | grep -oE '[0-9A-F-]{36}' | head -1 || true)"
if [ -z "$DEVICE_ID" ]; then
    DEVICE_TYPE="$(xcrun simctl list devicetypes | grep -o 'com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-[0-9]*-46mm' | sort -V | tail -1)"
    DEVICE_ID="$(xcrun simctl create "$DEVICE_NAME" "$DEVICE_TYPE" "$RUNTIME_ID")"
fi
xcrun simctl boot "$DEVICE_ID" 2>/dev/null || true
xcrun simctl bootstatus "$DEVICE_ID" -b >/dev/null
xcrun simctl terminate "$DEVICE_ID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl install "$DEVICE_ID" "$APP"
xcrun simctl launch "$DEVICE_ID" "$BUNDLE_ID" "${LAUNCH_ARGUMENTS[@]+"${LAUNCH_ARGUMENTS[@]}"}"
echo "DEVICE_ID=$DEVICE_ID"
