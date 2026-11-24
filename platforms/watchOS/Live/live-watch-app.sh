#!/usr/bin/env bash
# Live mode's host for the watchOS simulator: builds Watchious Live, the SwiftUI host without .NET (LiveRuntime.swift
# stands in for it), installs it in a simulator and starts it on the computer that runs dotnet run -- --live.
#   live-watch-app.sh <simulator UDID> [computer URL, default http://localhost:5293]
# A computer other than this Mac starts the live server with --urls=http://0.0.0.0:5293 and is given by its address.
# Apple silicon Macs only: the host is built for the arm64 simulator.
set -euo pipefail
SIMULATOR="${1:?usage: live-watch-app.sh <simulator UDID> [computer URL]}"
URL="${2:-http://localhost:5293}"
HERE="$(cd "$(dirname "$0")" && pwd)"
HOST_DIR="$HERE/../Host"
APP="${TMPDIR:-/tmp}/watchious-live/WatchiousLive.app"

rm -rf "$APP"
mkdir -p "$APP"
# DEBUG: live mode is for development, so the host shows what it doesn't know (an unknown control) in place.
xcrun --sdk watchsimulator swiftc \
    -target "arm64-apple-watchos27.0-simulator" \
    -parse-as-library \
    -Onone \
    -D DEBUG \
    -import-objc-header "$HOST_DIR/Watchious.h" \
    "$HOST_DIR"/*.swift \
    "$HERE/LiveRuntime.swift" \
    -o "$APP/WatchiousLive"
cp "$HERE/Info.plist" "$APP/Info.plist"
codesign --force --sign - "$APP" > /dev/null

# Boots the simulator when it isn't, and waits until it is ready to install. Simulator.app shows it where it is
# installed; the simulator runs without it too.
xcrun simctl bootstatus "$SIMULATOR" -b > /dev/null
open -a Simulator 2> /dev/null || echo "Simulator.app isn't installed: the simulator runs without a window." >&2
xcrun simctl install "$SIMULATOR" "$APP"
xcrun simctl launch --terminate-running-process "$SIMULATOR" com.kebechet.watchious.live "--watchious-live=$URL"
