#!/usr/bin/env bash
# Installs and launches a built watch app.
#   run-watch-app.sh simulator <app> <bundle id> [simulator name]
#   run-watch-app.sh device    <app> <bundle id> <device UDID or name>
# Environment:
#   WATCHIOUS_LAUNCH_ARGUMENTS     arguments for the launched app
#   WATCHIOUS_DEVICE_RETRY_DELAY   seconds between attempts on a watch (default 10)
set -euo pipefail

MODE="$1"
APP="$2"
BUNDLE_ID="$3"
TARGET="${4:-}"

# devicectl fails like this while the watch's Wi-Fi tunnel is down: the watch is asleep (4016), out of reach or just
# waking up (4000, the tunnel timeout), or not ready to mount the developer disk image (12040). Worth another try;
# anything else, such as a signing or provisioning error, is not.
REACH_ERRORS='CoreDeviceError error (4000|4016|12040)|RemotePairingError|establish tunnel'
DEVICE_ATTEMPTS=5

# Runs devicectl, again while the watch can't be reached; its output goes to stdout when it succeeds. Failed attempts go
# to stderr with devicectl's ERROR lines reworded, so that MSBuild doesn't count an attempt that a later one makes good
# as a build error; when every attempt fails, one error line says why.
devicectl_until_reached() {
    local action="$1"
    shift
    local attempt output reason
    for attempt in $(seq "$DEVICE_ATTEMPTS"); do
        if output="$(xcrun devicectl "$@" 2>&1)"; then
            printf '%s\n' "$output"
            return 0
        fi
        printf '%s\n' "$output" | sed -E 's/^([[:space:]]*)(ERROR|error):/\1devicectl:/' >&2
        if ! grep -qE "$REACH_ERRORS" <<<"$output"; then
            reason="$(grep -m1 -iE '^[[:space:]]*error:' <<<"$output" | sed -E 's/^[[:space:]]*(ERROR|error):[[:space:]]*//' || true)"
            echo "run-watch-app.sh : error : Couldn't $action: ${reason:-$(tail -1 <<<"$output")}" >&2
            return 1
        fi
        if [ "$attempt" -lt "$DEVICE_ATTEMPTS" ]; then
            echo "The watch can't be reached, trying again in ${WATCHIOUS_DEVICE_RETRY_DELAY:-10} s ($attempt/$DEVICE_ATTEMPTS): keep it awake, on the same Wi-Fi as the Mac, with Bluetooth on." >&2
            sleep "${WATCHIOUS_DEVICE_RETRY_DELAY:-10}"
        fi
    done
    echo "run-watch-app.sh : error : Couldn't $action: the watch wasn't reachable after $DEVICE_ATTEMPTS attempts. Wake it, keep it on the same Wi-Fi as the Mac with Bluetooth on, and check it in 'xcrun devicectl list devices'." >&2
    return 1
}

# The watch drops its tunnel when it dozes off between the steps of a long install; asking it for its lock state every
# 15 s keeps the tunnel up. Unlike its details, which devicectl answers from what it knows, the lock state needs the
# tunnel: it fails with error 4000 while the watch can't be reached.
keep_watch_awake() {
    while true; do
        sleep 15
        xcrun devicectl device info lockState --device "$TARGET" >/dev/null 2>&1 || true
    done
}

if [ "$MODE" = "device" ]; then
    if [ -z "$TARGET" ]; then
        echo "Set WatchiousDevice to the watch's UDID or name (see 'xcrun devicectl list devices')." >&2
        exit 2
    fi
    devicectl_until_reached "reach the watch $TARGET" device info lockState --device "$TARGET" >/dev/null
    # Its output goes nowhere: a sleep left running after the script would otherwise hold MSBuild's pipes open, and
    # MSBuild waits for them to close.
    keep_watch_awake >/dev/null 2>&1 &
    KEEP_AWAKE_PID=$!
    # Its sleep first, which would otherwise run on for up to 15 s after the script.
    trap 'pkill -P "$KEEP_AWAKE_PID" 2>/dev/null; kill "$KEEP_AWAKE_PID" 2>/dev/null || true' EXIT
    devicectl_until_reached "install the app on $TARGET" device install app --device "$TARGET" "$APP"
    devicectl_until_reached "launch the app on $TARGET" device process launch --terminate-existing --device "$TARGET" "$BUNDLE_ID" -- ${WATCHIOUS_LAUNCH_ARGUMENTS:-}
    exit 0
fi

DEVICE_NAME="${TARGET:-Watchious Watch}"
RUNTIME_ID="$(xcrun simctl list runtimes available | grep -o 'com.apple.CoreSimulator.SimRuntime.watchOS-[0-9-]*' | sort -V | tail -1 || true)"
if [ -z "$RUNTIME_ID" ]; then
    echo "No watchOS simulator runtime is installed (Xcode > Settings > Components)." >&2
    exit 2
fi
RUNTIME_VERSION="${RUNTIME_ID##*watchOS-}"
SIMULATOR_NAME="$DEVICE_NAME (watchOS ${RUNTIME_VERSION//-/.})"
DEVICE_ID="$(xcrun simctl list devices available | grep -F "$SIMULATOR_NAME (" | grep -oE '[0-9A-F-]{36}' | head -1 || true)"
if [ -z "$DEVICE_ID" ]; then
    DEVICE_TYPE="$(xcrun simctl list devicetypes | grep -o 'com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-[0-9]*-46mm' | sort -V | tail -1 || true)"
    if [ -z "$DEVICE_TYPE" ]; then
        echo "Xcode has no Apple Watch Series 46mm simulator to create \"$SIMULATOR_NAME\" from." >&2
        exit 2
    fi
    DEVICE_ID="$(xcrun simctl create "$SIMULATOR_NAME" "$DEVICE_TYPE" "$RUNTIME_ID")"
fi
xcrun simctl boot "$DEVICE_ID" 2>/dev/null || true
xcrun simctl bootstatus "$DEVICE_ID" -b >/dev/null
# Simulator.app shows the simulator; it runs without it too, as over ssh without anyone logged in to the Mac.
open -a Simulator 2>/dev/null || echo "Simulator.app couldn't be opened: the simulator runs without a window." >&2
xcrun simctl terminate "$DEVICE_ID" "$BUNDLE_ID" 2>/dev/null || true
xcrun simctl install "$DEVICE_ID" "$APP"
xcrun simctl launch "$DEVICE_ID" "$BUNDLE_ID" ${WATCHIOUS_LAUNCH_ARGUMENTS:-}
echo "Simulator: $SIMULATOR_NAME ($DEVICE_ID)"
