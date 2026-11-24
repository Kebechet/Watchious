#!/usr/bin/env bash
# Relinks the native output of .NET for iOS (NativeAOT code, Microsoft's runtime glue, the .NET runtime libraries)
# into a watchOS executable together with the SwiftUI host. Called by Watchious.Sdk.watchOS.targets, which passes:
#   WATCHIOUS_SDK_NAME        watchos or watchsimulator
#   WATCHIOUS_MIN_OS          minimum watchOS version
#   WATCHIOUS_HOST_DIR        SwiftUI host sources (*.swift, *.c, Watchious.h)
#   WATCHIOUS_TOOLS_DIR       this directory (retag-macho.py)
#   WATCHIOUS_WORK_DIR        intermediate directory
#   WATCHIOUS_OUTPUT          executable to write
#   WATCHIOUS_INPUTS_FILE     object files and static libraries .NET for iOS links, one per line
#   WATCHIOUS_FLAGS_FILE      its linker flags, one per line
#   WATCHIOUS_FRAMEWORKS_FILE its frameworks, one per line
#   WATCHIOUS_ENTITLEMENTS    the app's entitlements plist, if any
#   WATCHIOUS_BUNDLE_ID       the watch app's bundle identifier
#   WATCHIOUS_EXTRA_SOURCES_FILE Swift and C files optional packages add to the host, one per line
#   WATCHIOUS_CONFIGURATION   the build configuration (Debug compiles the host with DEBUG)
set -euo pipefail

SDK_ROOT="$(xcrun --sdk "$WATCHIOUS_SDK_NAME" --show-sdk-path)"
SDK_VERSION="$(xcrun --sdk "$WATCHIOUS_SDK_NAME" --show-sdk-version)"
if [ "$WATCHIOUS_SDK_NAME" = "watchsimulator" ]; then
    RETAG_TARGET="watchossimulator"
    TARGET_TRIPLE="arm64-apple-watchos${WATCHIOUS_MIN_OS}-simulator"
else
    RETAG_TARGET="watchos"
    TARGET_TRIPLE="arm64-apple-watchos${WATCHIOUS_MIN_OS}"
fi

INPUTS=()
FLAGS=()
FRAMEWORKS=()

add_input() {
    local input
    for input in "${INPUTS[@]+"${INPUTS[@]}"}"; do
        [ "$input" = "$1" ] && return
    done
    INPUTS+=("$1")
}

# A library or a linker option repeated is passed once; other flags stay as they come, since some come in pairs
# (-framework A -framework B).
add_flag() {
    local flag
    case "$1" in
        -l*|-Wl,*)
            for flag in "${FLAGS[@]+"${FLAGS[@]}"}"; do
                [ "$flag" = "$1" ] && return
            done
            ;;
    esac
    FLAGS+=("$1")
}

# .NET for iOS links iPhone-only frameworks too (UIKit, GSS); a watch app links only what watchOS has.
add_framework() {
    local framework
    if [ ! -d "$SDK_ROOT/System/Library/Frameworks/$1.framework" ]; then
        echo "Watchious: $1.framework doesn't exist on watchOS, so it isn't linked."
        return
    fi
    for framework in "${FRAMEWORKS[@]+"${FRAMEWORKS[@]}"}"; do
        [ "$framework" = "$1" ] && return
    done
    FRAMEWORKS+=("$1")
}

while IFS= read -r input || [ -n "$input" ]; do
    [ -n "$input" ] && add_input "$input"
done < "$WATCHIOUS_INPUTS_FILE"

skip_next=false
while IFS= read -r flag || [ -n "$flag" ]; do
    [ -z "$flag" ] && continue
    if [ "$skip_next" = true ]; then
        skip_next=false
        continue
    fi
    case "$flag" in
        # The symbol list keeps iPhone entry points alive; the watch app exports nothing.
        -exported_symbols_list) skip_next=true ;;
        # iPhone Swift library paths; swiftc adds the watchOS ones.
        -L*) ;;
        -dead_strip) ;;
        *.a|*.o) add_input "$flag" ;;
        -Wl,-framework,*) add_framework "${flag#-Wl,-framework,}" ;;
        # Apple SDKs no longer ship libstdc++; clang++ maps it to libc++, swiftc doesn't.
        -lstdc++) add_flag "-lc++" ;;
        *) add_flag "$flag" ;;
    esac
done < "$WATCHIOUS_FLAGS_FILE"

while IFS= read -r framework || [ -n "$framework" ]; do
    [ -n "$framework" ] && add_framework "$framework"
done < "$WATCHIOUS_FRAMEWORKS_FILE"

NATIVE_DIR="$WATCHIOUS_WORK_DIR/native"
HOST_OBJECT_DIR="$WATCHIOUS_WORK_DIR/host"
mkdir -p "$NATIVE_DIR" "$HOST_OBJECT_DIR"

python3 "$WATCHIOUS_TOOLS_DIR/retag-macho.py" --target "$RETAG_TARGET" --minos "$WATCHIOUS_MIN_OS" --sdk "$SDK_VERSION" \
    --output-dir "$NATIVE_DIR" "${INPUTS[@]}" > "$WATCHIOUS_WORK_DIR/retag.log"
RETAGGED=()
for input in "${INPUTS[@]}"; do
    RETAGGED+=("$NATIVE_DIR/$(basename "$input")")
done

EXTRA_SWIFT=()
EXTRA_C=()
if [ -n "${WATCHIOUS_EXTRA_SOURCES_FILE:-}" ] && [ -f "$WATCHIOUS_EXTRA_SOURCES_FILE" ]; then
    while IFS= read -r source || [ -n "$source" ]; do
        source="${source%$'\r'}"
        case "$source" in
            *.swift) EXTRA_SWIFT+=("$source") ;;
            *.c) EXTRA_C+=("$source") ;;
        esac
    done < "$WATCHIOUS_EXTRA_SOURCES_FILE"
fi

HOST_OBJECTS=()
for source in "$WATCHIOUS_HOST_DIR"/*.c "${EXTRA_C[@]+"${EXTRA_C[@]}"}"; do
    object="$HOST_OBJECT_DIR/$(basename "${source%.c}").o"
    xcrun --sdk "$WATCHIOUS_SDK_NAME" clang -target "$TARGET_TRIPLE" -O2 -I "$WATCHIOUS_HOST_DIR" -c "$source" -o "$object"
    HOST_OBJECTS+=("$object")
done

FRAMEWORK_FLAGS=()
for framework in "${FRAMEWORKS[@]+"${FRAMEWORKS[@]}"}"; do
    FRAMEWORK_FLAGS+=(-framework "$framework")
done

# In the simulator, entitlements (HealthKit, app groups...) are read from a section of the executable rather than
# from its ad hoc signature, so they go in at link time, as Xcode does it.
ENTITLEMENT_FLAGS=()
if [ "$WATCHIOUS_SDK_NAME" = "watchsimulator" ] && [ -n "${WATCHIOUS_ENTITLEMENTS:-}" ]; then
    plutil -convert xml1 -o "$WATCHIOUS_WORK_DIR/simulator.entitlements" "$WATCHIOUS_ENTITLEMENTS"
    # Capabilities such as HealthKit refuse an app without an application identifier, which on a watch comes from
    # the provisioning profile. The simulator has none and checks no team, so a placeholder team prefix does.
    if ! plutil -extract application-identifier raw "$WATCHIOUS_WORK_DIR/simulator.entitlements" > /dev/null 2>&1; then
        plutil -insert application-identifier -string "FAKETEAMID.${WATCHIOUS_BUNDLE_ID:?}" "$WATCHIOUS_WORK_DIR/simulator.entitlements"
    fi
    ENTITLEMENT_FLAGS=(-Xlinker -sectcreate -Xlinker __TEXT -Xlinker __entitlements
        -Xlinker "$WATCHIOUS_WORK_DIR/simulator.entitlements")
fi

# A debug build shows what the host doesn't know (an unknown control) in place, not only in the log.
DEBUG_FLAGS=()
# Without case, as MSBuild compares the configuration.
if [ "$(printf '%s' "${WATCHIOUS_CONFIGURATION:-}" | tr '[:upper:]' '[:lower:]')" = "debug" ]; then
    DEBUG_FLAGS=(-D DEBUG)
fi

# .NET for iOS's generated main.arm64.o defines an iPhone main(); the SwiftUI host gets its own entry point name.
mkdir -p "$(dirname "$WATCHIOUS_OUTPUT")"
xcrun --sdk "$WATCHIOUS_SDK_NAME" swiftc \
    -target "$TARGET_TRIPLE" \
    -parse-as-library \
    -O \
    "${DEBUG_FLAGS[@]+"${DEBUG_FLAGS[@]}"}" \
    -import-objc-header "$WATCHIOUS_HOST_DIR/Watchious.h" \
    -Xfrontend -entry-point-function-name -Xfrontend watchious_main \
    "$WATCHIOUS_HOST_DIR"/*.swift \
    "${EXTRA_SWIFT[@]+"${EXTRA_SWIFT[@]}"}" \
    "${HOST_OBJECTS[@]}" \
    "${RETAGGED[@]}" \
    "${FRAMEWORK_FLAGS[@]+"${FRAMEWORK_FLAGS[@]}"}" \
    "${FLAGS[@]+"${FLAGS[@]}"}" \
    "${ENTITLEMENT_FLAGS[@]+"${ENTITLEMENT_FLAGS[@]}"}" \
    -Xlinker -e -Xlinker _watchious_main \
    -Xlinker -dead_strip \
    -o "$WATCHIOUS_OUTPUT"
