#!/usr/bin/env bash
# Builds Watchious Live for watchOS, live mode's host: the SwiftUI host compiled with LiveRuntime.swift, which stands in
# for the app's .NET code and talks to the app running on a computer. Called by the WatchiousLive target
# (Watchious.Sdk.watchOS.targets), which passes:
#   WATCHIOUS_SDK_NAME        watchos or watchsimulator
#   WATCHIOUS_MIN_OS          minimum watchOS version
#   WATCHIOUS_HOST_DIR        SwiftUI host sources (*.swift, Watchious.h)
#   WATCHIOUS_LIVE_DIR        LiveRuntime.swift and the live host's Info.plist
#   WATCHIOUS_TOOLS_DIR       this directory (complete-watch-app.sh)
#   WATCHIOUS_WORK_DIR        intermediate directory
#   WATCHIOUS_APP             the .app bundle to write
#   WATCHIOUS_BUNDLE_ID       its bundle identifier
#   WATCHIOUS_IMAGES_FILE     the app's images (<Image Source>), one per line
#   WATCHIOUS_SIGN_IDENTITY   signing identity and
#   WATCHIOUS_PROFILE         provisioning profile, for a real watch; the simulator's is signed ad hoc
set -euo pipefail

APP="${WATCHIOUS_APP%/}"
if [ "$WATCHIOUS_SDK_NAME" = "watchsimulator" ]; then
    TARGET_TRIPLE="arm64-apple-watchos${WATCHIOUS_MIN_OS}-simulator"
else
    TARGET_TRIPLE="arm64-apple-watchos${WATCHIOUS_MIN_OS}"
fi

rm -rf "$APP"
mkdir -p "$APP" "$WATCHIOUS_WORK_DIR"
# Only the host's Swift: its C files start .NET, which the live host doesn't have. DEBUG: live mode is for development,
# so the host shows what it doesn't know (an unknown control) in place.
xcrun --sdk "$WATCHIOUS_SDK_NAME" swiftc \
    -target "$TARGET_TRIPLE" \
    -parse-as-library \
    -Onone \
    -D DEBUG \
    -import-objc-header "$WATCHIOUS_HOST_DIR/Watchious.h" \
    "$WATCHIOUS_HOST_DIR"/*.swift \
    "$WATCHIOUS_LIVE_DIR/LiveRuntime.swift" \
    -o "$APP/WatchiousLive"
cp "$WATCHIOUS_LIVE_DIR/Info.plist" "$APP/Info.plist"

# SwiftUI loads them with UIImage(named:) from the bundle, as in the app.
while IFS= read -r image || [ -n "$image" ]; do
    image="${image%$'\r'}"
    [ -n "$image" ] && cp "$image" "$APP/"
done < "$WATCHIOUS_IMAGES_FILE"

# The watchOS Info.plist keys and the signature, as for the app. Debug: the host reaches the computer over plain HTTP
# on the local network (NSAllowsLocalNetworking).
WATCHIOUS_APP="$APP" \
WATCHIOUS_INFO_PLIST="" \
WATCHIOUS_ASSETS="" \
WATCHIOUS_ENTITLEMENTS="" \
WATCHIOUS_COMPANION_ID="" \
WATCHIOUS_CONFIGURATION=Debug \
    bash "$WATCHIOUS_TOOLS_DIR/complete-watch-app.sh"
