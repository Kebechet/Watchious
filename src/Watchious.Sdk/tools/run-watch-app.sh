#!/usr/bin/env bash
# Installs and launches a built watch app.
#   run-watch-app.sh simulator <app> <bundle id> [simulator name]
#   run-watch-app.sh device    <app> <bundle id> <device UDID or name>
set -euo pipefail

MODE="$1"
APP="$2"
BUNDLE_ID="$3"
TARGET="${4:-}"

if [ "$MODE" = "device" ]; then
    if [ -z "$TARGET" ]; then
        echo "Set WatchiousDevice to the watch's UDID or name (see 'xcrun devicectl list devices')." >&2
        exit 2
    fi
    xcrun devicectl device install app --device "$TARGET" "$APP"
    xcrun devicectl device process launch --terminate-existing --device "$TARGET" "$BUNDLE_ID" -- ${WATCHIOUS_LAUNCH_ARGUMENTS:-}
    exit 0
fi

DEVICE_NAME="${TARGET:-Watchious Watch}"
RUNTIME_ID="$(xcrun simctl list runtimes available | grep -o 'com.apple.CoreSimulator.SimRuntime.watchOS-[0-9-]*' | sort -V | tail -1)"
if [ -z "$RUNTIME_ID" ]; then
    echo "No watchOS simulator runtime is installed (Xcode > Settings > Components)." >&2
    exit 2
fi
RUNTIME_VERSION="${RUNTIME_ID##*watchOS-}"
SIMULATOR_NAME="$DEVICE_NAME (watchOS ${RUNTIME_VERSION//-/.})"
DEVICE_ID="$(xcrun simctl list devices available | grep -F "$SIMULATOR_NAME (" | grep -oE '[0-9A-F-]{36}' | head -1 || true)"
if [ -z "$DEVICE_ID" ]; then
    DEVICE_TYPE="$(xcrun simctl list devicetypes | grep -o 'com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-[0-9]*-46mm' | sort -V | tail -1)"
    DEVICE_ID="$(xcrun simctl create "$SIMULATOR_NAME" "$DEVICE_TYPE" "$RUNTIME_ID")"
fi
xcrun simctl boot "$DEVICE_ID" 2>/dev/null || true
xcrun simctl bootstatus "$DEVICE_ID" -b >/dev/null
xcrun simctl terminate "$DEVICE_ID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl install "$DEVICE_ID" "$APP"
xcrun simctl launch "$DEVICE_ID" "$BUNDLE_ID" ${WATCHIOUS_LAUNCH_ARGUMENTS:-}
echo "Simulator: $SIMULATOR_NAME ($DEVICE_ID)"
