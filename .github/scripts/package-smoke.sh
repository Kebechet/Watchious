#!/usr/bin/env bash
# Tests the packed Kebechet.Watchious.* packages the way an app outside this repository uses them. Inside the
# repository nothing uses them: Directory.Build.props switches the SDK to ProjectReferences, and the samples import the
# SDK by path. So this creates apps from the template content in a folder outside the repository, with a nuget.config
# that clears every other package source, and checks:
#   - restore takes exactly the Kebechet.Watchious.* packages of this version, and no old Watchious.* id;
#   - the SDK comes from its package (WatchiousVersion, WatchiousSdkDirectory);
#   - an app with Central Package Management restores (the SDK's implicit references, NU1008 otherwise);
#   - Kebechet.Watchious.Health and .Messaging add their Swift files to the watchOS host (WatchiousWatchOSHostSource);
#   - the app builds for net10.0 (the desktop preview), and for net10.0-android in Release (R8), with the optional
#     packages;
#   - with --watchos (macOS only), it builds for the watchOS simulator instead of Wear OS, with the optional packages,
#     and their Swift files go into the watch app. That build needs Xcode and the ios workload; the Wear OS one a JDK
#     and the Android SDK. All restores need the android workload, since the template targets Wear OS everywhere.
# Runs in bash on Linux and macOS and in Git Bash on Windows:
#
#   .github/scripts/package-smoke.sh [--watchos] <package folder> <version> [work folder, default a new temp folder]
#
#   DOTNET          dotnet to build with (default: dotnet)
#   NUGET_PACKAGES  NuGet's global packages folder, as for dotnet itself
#
# NuGet keeps a restored package version in its global packages folder for good, so a test of a newly packed version
# must use a version that isn't there yet: the run fails when it is. Pack the packages to test with a throwaway version
# such as 0.0.0-smoke.<n> (.github/scripts/pack.sh 0.0.0-smoke.<n>), never a real version.
set -euo pipefail

WATCHOS=false
if [ "${1:-}" = "--watchos" ]; then
    WATCHOS=true
    shift
fi
if [ $# -lt 2 ] || [ $# -gt 3 ]; then
    echo "usage: $0 [--watchos] <package folder> <version> [work folder]" >&2
    exit 2
fi
FEED_ARG="$1"
VERSION="$2"
WORK_ARG="${3:-}"
DOTNET="${DOTNET:-dotnet}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TEMPLATE="$ROOT/src/Watchious.Templates/content/watchious"
IS_MACOS=false
[ "$(uname -s)" = "Darwin" ] && IS_MACOS=true

fail() {
    echo "::error::package smoke: $*" >&2
    exit 1
}

# dotnet is a Windows program in Git Bash, so paths it gets are in Windows form (pwd -W exists only there).
native_path() {
    if pwd -W > /dev/null 2>&1; then (cd "$1" && pwd -W); else (cd "$1" && pwd); fi
}

if [ "$WATCHOS" = true ] && [ "$IS_MACOS" != true ]; then
    fail "--watchos needs macOS (Xcode and the ios workload)."
fi
[ -d "$FEED_ARG" ] || fail "no package folder $FEED_ARG."
[ -f "$FEED_ARG/Kebechet.Watchious.Sdk.$VERSION.nupkg" ] || fail "no Kebechet.Watchious.Sdk.$VERSION.nupkg in $FEED_ARG."
FEED="$(native_path "$FEED_ARG")"

if [ -z "$WORK_ARG" ]; then
    WORK_ARG="$(mktemp -d "${TMPDIR:-/tmp}/watchious-smoke.XXXXXX")"
fi
mkdir -p "$WORK_ARG"
WORK_ABS="$(cd "$WORK_ARG" && pwd)"
ROOT_ABS="$(cd "$ROOT" && pwd)"
case "$WORK_ABS/" in
    "$ROOT_ABS"/*) fail "the work folder $WORK_ABS is inside the repository, whose Directory.Build.props would switch the SDK to the source projects." ;;
esac
# A Directory.Build.props, Directory.Build.targets or Directory.Packages.props above the apps would change them.
dir="$WORK_ABS"
while :; do
    for file in Directory.Build.props Directory.Build.targets Directory.Packages.props; do
        [ ! -f "$dir/$file" ] || fail "$dir/$file would apply to the test apps; use a work folder outside it."
    done
    parent="$(dirname "$dir")"
    [ "$parent" != "$dir" ] || break
    dir="$parent"
done
WORK="$(native_path "$WORK_ABS")"
LOGS="$WORK_ABS/logs"
mkdir -p "$LOGS"
echo "== package smoke: $VERSION from $FEED, in $WORK"

GLOBAL_PACKAGES="$("$DOTNET" nuget locals global-packages --list | tr -d '\r' | sed -n 's/^global-packages: //p')"
[ -n "$GLOBAL_PACKAGES" ] || fail "dotnet nuget locals didn't name the global packages folder."
VERSION_LOWER="$(printf '%s' "$VERSION" | tr '[:upper:]' '[:lower:]')"
if [ -d "$GLOBAL_PACKAGES/kebechet.watchious.sdk/$VERSION_LOWER" ]; then
    fail "Kebechet.Watchious.Sdk $VERSION is already in NuGet's global packages folder ($GLOBAL_PACKAGES), so restore would take that copy instead of the one in $FEED. Pack a new version (for example the next 0.0.0-smoke.<n>)."
fi

# Kebechet.Watchious.* only from the packed packages, everything else from nuget.org.
cat > "$WORK_ABS/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="watchious" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="watchious">
      <package pattern="Kebechet.Watchious.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

# An app from the template content, with the SDK at this version. Until the template package stamps its own version
# (https://github.com/Kebechet/Watchious/issues/12), the content names a fixed one.
new_app() {
    local name="$1" app="$WORK_ABS/$1"
    [ ! -e "$app" ] || fail "$app already exists; use a new work folder."
    mkdir -p "$app"
    tar -C "$TEMPLATE" --exclude=./.template.config --exclude=./bin --exclude=./obj --exclude=./WatchiousApp1.csproj \
        -cf - . | tar -C "$app" -xf -
    sed "s|Sdk=\"Kebechet.Watchious.Sdk/[^\"]*\"|Sdk=\"Kebechet.Watchious.Sdk/$VERSION\"|" "$TEMPLATE/WatchiousApp1.csproj" \
        > "$app/$name.csproj"
    grep -q "<Project Sdk=\"Kebechet.Watchious.Sdk/$VERSION\">" "$app/$name.csproj" \
        || fail "the template's WatchiousApp1.csproj doesn't start with <Project Sdk=\"Kebechet.Watchious.Sdk/...\">."
}

# Adds lines before the project's closing tag.
add_to_project() {
    local project="$1" lines="$2"
    awk -v lines="$lines" '/^<\/Project>/ { print lines } { print }' "$project" > "$project.new"
    mv "$project.new" "$project"
}

run() {
    local log="$1"
    shift
    echo "== $*"
    "$@" 2>&1 | tee "$LOGS/$log.log"
}

property() {
    "$DOTNET" msbuild "$1" -nologo "-getProperty:$2" "${@:3}" | tr -d '\r'
}

# Every Kebechet.Watchious.* package in the restore is of this version, the set is exactly the expected one, and no
# old Watchious.* id is there (an id the rename missed, which anyone could publish on nuget.org).
check_restore() {
    local app="$1" assets="$WORK_ABS/$1/obj/project.assets.json"
    shift
    [ -f "$assets" ] || fail "$app: no obj/project.assets.json after the restore."
    local old
    old="$(grep -oE '"Watchious\.[A-Za-z0-9.]+(/[^"]*)?"' "$assets" | sort -u || true)"
    [ -z "$old" ] || fail "$app: the restore has old Watchious.* ids: $(echo $old)"
    local resolved
    resolved="$(grep -oE '"Kebechet\.Watchious\.[A-Za-z0-9.]+/[^"]+"' "$assets" | tr -d '"' | sort -u || true)"
    local wrong
    wrong="$(printf '%s\n' "$resolved" | grep -v "/$VERSION\$" || true)"
    [ -z "$wrong" ] || fail "$app: packages of another version than $VERSION: $(echo $wrong)"
    local ids expected
    ids="$(printf '%s\n' "$resolved" | cut -d/ -f1 | sort -u | tr '\n' ' ')"
    expected="$(printf 'Kebechet.Watchious.%s\n' "$@" | sort -u | tr '\n' ' ')"
    [ "$ids" = "$expected" ] || fail "$app: the restore has [$ids], expected [$expected]."
    echo "== $app restored, all at $VERSION: $ids"
}

# What the SDK adds for each target framework the template has here: the preview (net10.0), Wear OS, and watchOS on a
# Mac (the template adds net10.0-watchos only there).
BASE_PACKAGES=(Controls Core Essentials Preview Hosts.WearOS)
[ "$IS_MACOS" = true ] && BASE_PACKAGES+=(Hosts.watchOS)

# 1. The template as it is.
new_app WatchiousApp1
APP1="$WORK/WatchiousApp1/WatchiousApp1.csproj"
run restore-app "$DOTNET" restore "$APP1" -nologo
check_restore WatchiousApp1 "${BASE_PACKAGES[@]}"
use_project_references="$(property "$APP1" WatchiousUseProjectReferences -p:TargetFramework=net10.0)"
[ -z "$use_project_references" ] || fail "WatchiousApp1 uses the SDK's source projects (WatchiousUseProjectReferences=$use_project_references)."
watchious_version="$(property "$APP1" WatchiousVersion -p:TargetFramework=net10.0)"
[ "$watchious_version" = "$VERSION" ] || fail "the SDK's WatchiousVersion is '$watchious_version', expected $VERSION."
sdk_directory="$(property "$APP1" WatchiousSdkDirectory -p:TargetFramework=net10.0)"
printf '%s' "$sdk_directory" | tr '\\' '/' | grep -qi "kebechet.watchious.sdk/$VERSION_LOWER/\$" \
    || fail "the SDK isn't the package: WatchiousSdkDirectory is '$sdk_directory'."
echo "== the SDK is the package: $sdk_directory"
run build-preview "$DOTNET" build "$APP1" -f net10.0 -nologo

# 2. With the optional packages, at the SDK's version.
new_app WatchiousOptional
APP2="$WORK/WatchiousOptional/WatchiousOptional.csproj"
add_to_project "$WORK_ABS/WatchiousOptional/WatchiousOptional.csproj" '
  <ItemGroup>
    <PackageReference Include="Kebechet.Watchious.Health" Version="$(WatchiousVersion)" />
    <PackageReference Include="Kebechet.Watchious.Messaging" Version="$(WatchiousVersion)" />
  </ItemGroup>
'
run restore-optional "$DOTNET" restore "$APP2" -nologo
check_restore WatchiousOptional "${BASE_PACKAGES[@]}" Health Messaging
host_sources="$("$DOTNET" msbuild "$APP2" -nologo -getItem:WatchiousWatchOSHostSource -p:TargetFramework=net10.0 | tr -d '\r' | tr -s '\\/' '/')"
printf '%s\n' "$host_sources" > "$LOGS/host-sources.json"
printf '%s\n' "$host_sources" | grep -qiE "kebechet\.watchious\.health/$VERSION_LOWER/.*HealthKitWorkout\.swift" \
    || fail "Kebechet.Watchious.Health added no HealthKitWorkout.swift to WatchiousWatchOSHostSource (is its build/ targets file named after the package?)."
printf '%s\n' "$host_sources" | grep -qiE "kebechet\.watchious\.messaging/$VERSION_LOWER/.*WatchConnectivity\.swift" \
    || fail "Kebechet.Watchious.Messaging added no WatchConnectivity.swift to WatchiousWatchOSHostSource (is its build/ targets file named after the package?)."
echo "== Health and Messaging add their Swift to the watchOS host"
if [ "$WATCHOS" != true ]; then
    link_tool="$(property "$APP2" AndroidLinkTool -p:TargetFramework=net10.0-android -p:Configuration=Release)"
    [ "$link_tool" = "r8" ] || fail "a Release build for Wear OS doesn't use R8 (AndroidLinkTool='$link_tool')."
    run build-android-release "$DOTNET" build "$APP2" -f net10.0-android -c Release -nologo
    apks="$(find "$WORK_ABS/WatchiousOptional/bin/Release/net10.0-android" -name '*-Signed.apk' 2>/dev/null || true)"
    [ -n "$apks" ] || fail "the Release build for Wear OS made no signed APK."
    echo "== Wear OS Release APK: $apks"
else
    run build-watchos "$DOTNET" build "$APP2" -f net10.0-watchos -nologo
    linked="$(find "$WORK_ABS/WatchiousOptional/obj" -path '*net10.0-watchos*' -name host-sources.txt 2>/dev/null | head -1)"
    [ -n "$linked" ] || fail "the watchOS build wrote no host-sources.txt: the watch app wasn't linked."
    grep -q 'HealthKitWorkout\.swift' "$linked" && grep -q 'WatchConnectivity\.swift' "$linked" \
        || fail "the watchOS host was linked without the Health and Messaging Swift ($linked)."
    app_bundle="$(find "$WORK_ABS/WatchiousOptional/bin" -path '*net10.0-watchos*' -name '*.app' -type d 2>/dev/null | head -1)"
    [ -n "$app_bundle" ] || fail "the watchOS build made no .app."
    echo "== watchOS simulator app: $app_bundle"
fi

# 3. Central Package Management: the SDK's references are implicit, and the app's own come from
# Directory.Packages.props.
new_app WatchiousCpm
cat > "$WORK_ABS/WatchiousCpm/Directory.Packages.props" <<EOF
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Kebechet.Watchious.Messaging" Version="$VERSION" />
  </ItemGroup>
</Project>
EOF
add_to_project "$WORK_ABS/WatchiousCpm/WatchiousCpm.csproj" '
  <ItemGroup>
    <PackageReference Include="Kebechet.Watchious.Messaging" />
  </ItemGroup>
'
run restore-cpm "$DOTNET" restore "$WORK/WatchiousCpm/WatchiousCpm.csproj" -nologo
check_restore WatchiousCpm "${BASE_PACKAGES[@]}" Messaging

echo "== package smoke passed: $VERSION (logs in $LOGS)"
