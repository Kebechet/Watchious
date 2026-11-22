#!/usr/bin/env bash
# The watchOS canary: builds the Counter sample for the watchOS simulator with the dotnet on PATH (or $DOTNET),
# starts it, clicks "+1" and checks that the page shows "Count: 1". Watchious starts .NET for iOS through internal
# functions (platforms/watchOS/Host/StartRuntime.c) that a new .NET for iOS version can change, so this runs on each
# new version before it goes into WatchiousTestedIOSSdkVersions. Used by .github/workflows/watchos-canary.yml, and
# runnable on any Mac with Xcode and the ios and android workloads:
#
#   .github/scripts/watchos-canary.sh [app .NET, default net10.0]
#
#   DOTNET                    dotnet to build with (default: dotnet)
#   CANARY_LOG_DIR            where the build and app logs go (default: artifacts/canary)
#   WATCHIOUS_ALLOW_UNTESTED  build even if the .NET for iOS version isn't a tested one (default: true)
set -euo pipefail

APP_DOTNET="${1:-net10.0}"
DOTNET="${DOTNET:-dotnet}"
LOG_DIR="${CANARY_LOG_DIR:-artifacts/canary}"
PROJECT=samples/Watchious.Samples.Counter.App/Watchious.Samples.Counter.App.csproj
# The sample's ApplicationId plus .watchkitapp (WatchiousBundleIdentifier).
BUNDLE_ID=com.kebechet.watchious.counter.watchkitapp
mkdir -p "$LOG_DIR"

echo "== .NET $("$DOTNET" --version), $(xcodebuild -version | head -1), app $APP_DOTNET"
"$DOTNET" workload list | grep -E '^(ios|android|maui)' || true
START_MARKER="$LOG_DIR/.started"
touch "$START_MARKER"

# Run installs the app on its own watch simulator and launches it with these arguments. MSBuild unescapes %25 to %,
# and the app decodes %20 to the space in "Count: 1".
"$DOTNET" build "$PROJECT" -f "$APP_DOTNET-watchos" -t:Run -nologo \
    "-p:WatchiousAppDotNet=$APP_DOTNET" \
    "-p:WatchiousAllowUntestedIOSSdk=${WATCHIOUS_ALLOW_UNTESTED:-true}" \
    "-p:WatchiousLaunchArguments=--watchious-click=+1 --watchious-expect=Count:%25201" \
    2>&1 | tee "$LOG_DIR/build.log"

# Run reports the simulator it launched the app on: "Simulator: <name> (<UDID>)".
DEVICE_ID="$(grep -o 'Simulator: .*' "$LOG_DIR/build.log" | tail -1 | grep -oE '[0-9A-F-]{36}' || true)"
if [ -z "$DEVICE_ID" ]; then
    echo "::error::The build didn't launch the app on a simulator (no 'Simulator:' line in $LOG_DIR/build.log)."
    exit 1
fi

# The automation clicks about 3 s after launch and writes each step to tmp/watchious-automation.log in the app's
# container. Not the unified log: on a fresh CI simulator, `log show` prints the app's lines as <compose failure>.
RESULTS="$LOG_DIR/automation.log"
: > "$RESULTS"
for _ in $(seq 12); do
    sleep 5
    CONTAINER="$(xcrun simctl get_app_container "$DEVICE_ID" "$BUNDLE_ID" data 2>/dev/null || true)"
    if [ -n "$CONTAINER" ] && [ -f "$CONTAINER/tmp/watchious-automation.log" ]; then
        cp "$CONTAINER/tmp/watchious-automation.log" "$RESULTS"
        if grep -q '\[Watchious\] expect Count: 1: ' "$RESULTS"; then
            break
        fi
    fi
done
xcrun simctl spawn "$DEVICE_ID" log show --last 5m --style compact --predicate 'process CONTAINS "Watchious"' \
    > "$LOG_DIR/app.log" 2>&1 || true
find "$HOME/Library/Logs/DiagnosticReports" -name 'Watchious*' -newer "$START_MARKER" -exec cp {} "$LOG_DIR/" \; 2>/dev/null || true

failures=()
grep -q '\[Watchious\] click +1 ' "$RESULTS" \
    || failures+=("the page never showed its +1 button: the app didn't start or C# didn't render")
grep -q '\[Watchious\] expect Count: 1: found' "$RESULTS" \
    || failures+=("after the click the page didn't show 'Count: 1': the click didn't go through C# and back")
if ls "$LOG_DIR"/Watchious*.ips > /dev/null 2>&1; then
    failures+=("the app crashed (crash report in $LOG_DIR)")
fi

if [ ${#failures[@]} -gt 0 ]; then
    for failure in "${failures[@]}"; do
        echo "::error::watchOS canary ($APP_DOTNET): $failure."
    done
    echo "== automation results:"
    cat "$RESULTS"
    echo "== last lines of the app log:"
    tail -40 "$LOG_DIR/app.log"
    exit 1
fi
echo "== watchOS canary ($APP_DOTNET): the app started, rendered its page and handled a click."
