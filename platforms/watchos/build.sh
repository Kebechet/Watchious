#!/usr/bin/env bash
# Builds the Counter sample for the watchOS simulator: NativeAOT static library -> re-tagged for watchOS ->
# linked into the SwiftUI host -> .app bundle. Pass --install to (re)install and launch it on a simulator.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${WATCHIOUS_OUT:-$HOME/watchious/out}"
CONFIGURATION="${CONFIGURATION:-Release}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
MIN_WATCHOS="26.0"
RID="iossimulator-arm64"
SDK_NAME="watchsimulator"
TARGET_TRIPLE="arm64-apple-watchos${MIN_WATCHOS}-simulator"
APP_NAME="Watchious"
EXECUTABLE="WatchiousHost"
BUNDLE_ID="com.kebechet.watchious.counter"
DEVICE_NAME="Watchious Watch"

INSTALL=false
LAUNCH_ARGUMENTS=()
for argument in "$@"; do
    case "$argument" in
        --install) INSTALL=true ;;
        *) LAUNCH_ARGUMENTS+=("$argument") ;;
    esac
done

export DOTNET_ROOT="$(dirname "$DOTNET")" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
PROJECT="$HERE/../../samples/Watchious.Samples.Counter.WatchOS/Watchious.Samples.Counter.WatchOS.csproj"
SDK_VERSION="$(xcrun --sdk "$SDK_NAME" --show-sdk-version)"

echo "==> NativeAOT static library ($RID)"
"$DOTNET" publish "$PROJECT" -c "$CONFIGURATION" -r "$RID" -o "$OUT/nativeaot" -nologo -v:q
RUNTIME_VERSION="$("$DOTNET" msbuild "$PROJECT" -getProperty:BundledNETCoreAppPackageVersion)"
NATIVE_DIR="$HOME/.nuget/packages/microsoft.netcore.app.runtime.nativeaot.$RID/$RUNTIME_VERSION/runtimes/$RID/native"

NATIVE_INPUTS=(
    "$OUT/nativeaot/WatchiousCounter.a"
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
python3 "$HERE/build/retag-macho.py" --target watchossimulator --minos "$MIN_WATCHOS" --sdk "$SDK_VERSION" \
    --output-dir "$OUT/watchos-libs" "${NATIVE_INPUTS[@]}"
RETAGGED=()
for input in "${NATIVE_INPUTS[@]}"; do
    RETAGGED+=("$OUT/watchos-libs/$(basename "$input")")
done

echo "==> Link SwiftUI host"
APP="$OUT/$APP_NAME.app"
mkdir -p "$APP"
xcrun --sdk "$SDK_NAME" swiftc \
    -target "$TARGET_TRIPLE" \
    -parse-as-library \
    -O \
    -import-objc-header "$HERE/Host/Watchious.h" \
    "$HERE"/Host/*.swift \
    "${RETAGGED[@]}" \
    -framework CoreFoundation -framework CryptoKit -framework Foundation -framework Network -framework Security \
    -lobjc -lswiftCore -lswiftFoundation -licucore -lz -lm \
    -Xlinker -dead_strip \
    -o "$APP/$EXECUTABLE"
cp "$HERE/Host/Info.plist" "$APP/Info.plist"
codesign --force --sign - --timestamp=none "$APP"

echo "==> Sizes"
du -sh "$OUT/nativeaot/WatchiousCounter.a" "$APP" "$APP/$EXECUTABLE"

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
