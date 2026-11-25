#!/usr/bin/env bash
# Tests the packed Kebechet.Watchious.* packages the way an app outside this repository uses them. Inside the
# repository nothing uses them: Directory.Build.props switches the SDK to ProjectReferences, and the samples import the
# SDK by path. So this installs the packed template, Kebechet.Watchious.Templates, into a template folder of its own
# (dotnet new --debug:custom-hive, so the templates installed on the machine stay as they are), creates apps with
# dotnet new watchious in a folder outside the repository, with a nuget.config that clears every other package source,
# and checks:
#   - an app from the template names Kebechet.Watchious.Sdk of this version
#     (https://github.com/Kebechet/Watchious/issues/12), and --sdk-version names another; a template installed from its
#     source folder has no version of its own, so its app's restore fails (MSB4236) unless --sdk-version names one;
#   - restore takes exactly the Kebechet.Watchious.* packages of this version, and no old Watchious.* id;
#   - the SDK comes from its package (WatchiousVersion, WatchiousSdkDirectory);
#   - an app with Central Package Management restores (the SDK's implicit references, NU1008 otherwise), also when
#     Directory.Packages.props versions the packages the SDK adds, for another project (NU1009 otherwise);
#   - Kebechet.Watchious.Health and .Messaging add their Swift files to the watchOS host (WatchiousWatchOSHostSource);
#   - the app builds for net10.0 (the desktop preview), and the built preview and inspector serve their pages;
#   - the app builds for net10.0-android in Release (R8), with the optional packages and the code the packages' READMEs
#     show (registering Messaging and Health, injecting their services and an Essentials one);
#   - with --watchos (macOS only), it builds for the watchOS simulator instead of Wear OS, with the optional packages
#     and that code, and their Swift files go into the watch app; and an untested .NET for iOS version fails the build
#     with WATCHIOUS003 unless WatchiousAllowUntestedIOSSdk is true (the SDK's README). That build needs Xcode and the
#     ios workload; the Wear OS one a JDK and the Android SDK. All restores need the android workload, since the
#     template targets Wear OS everywhere;
#   - live mode (dotnet build -t:WatchiousLive) takes its hosts from the SDK package; for the preview it says which
#     target framework to run it for, and how to start the app on the port it is given (WatchiousLivePort), and for
#     an Apple Watch it refuses a localhost address. On Wear OS it finds adb and asks it for the device it is given,
#     one that doesn't exist, so that no emulator or watch connected here is touched, and adb answers; with --watchos
#     it builds the watchOS live host for the simulator from the SDK package's sources, without starting it.
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
TEMPLATE_SOURCE="$ROOT/src/Watchious.Templates/content/watchious"
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
for package in Sdk Templates; do
    [ -f "$FEED_ARG/Kebechet.Watchious.$package.$VERSION.nupkg" ] || fail "no Kebechet.Watchious.$package.$VERSION.nupkg in $FEED_ARG."
done
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

TEMPLATE_HIVE="$WORK/template-hive"
TEMPLATE_SOURCE_HIVE="$WORK/template-hive-source"

# new_app <name> <template folder> <SDK version the project must name> [dotnet new options]: an app from the template
# installed in that template folder.
new_app() {
    local name="$1" hive="$2" sdk_version="$3" app="$WORK_ABS/$1" first_line
    shift 3
    [ ! -e "$app" ] || fail "$app already exists; use a new work folder."
    run "new-$name" "$DOTNET" new watchious -n "$name" -o "$WORK/$name" --no-update-check --debug:custom-hive "$hive" "$@"
    [ -f "$app/$name.csproj" ] || fail "dotnet new watchious made no $name.csproj."
    first_line="$(head -1 "$app/$name.csproj" | tr -d '\r')"
    [ "$first_line" = "<Project Sdk=\"Kebechet.Watchious.Sdk/$sdk_version\">" ] \
        || fail "$name.csproj from the template starts with '$first_line', expected <Project Sdk=\"Kebechet.Watchious.Sdk/$sdk_version\">."
    echo "== $name.csproj names Kebechet.Watchious.Sdk/$sdk_version"
}

# Adds lines before the project's closing tag.
add_to_project() {
    local project="$1" lines="$2"
    # Through the environment: macOS's awk refuses a newline in a -v value.
    SMOKE_LINES="$lines" awk '/^<\/Project>/ { print ENVIRON["SMOKE_LINES"] } { print }' "$project" > "$project.new"
    mv "$project.new" "$project"
}

run() {
    local log="$1"
    shift
    echo "== $*"
    "$@" 2>&1 | tee "$LOGS/$log.log"
}

# Starts a built app in the background and waits up to a minute for its page at the URL to contain the text; then
# stops the app.
check_served() {
    local log="$1" url="$2" text="$3" pid served=false
    shift 3
    echo "== $* ($url)"
    # The first answer can come before the app has even tried to listen, so another server on the port would pass.
    if curl -s "$url" > /dev/null 2>&1; then
        fail "$url is already served by another process, a preview left by an earlier run perhaps: stop it first."
    fi
    "$@" > "$LOGS/$log.log" 2>&1 &
    pid=$!
    for _ in $(seq 1 60); do
        # Not grep -q: it stops reading at the match, so on a page larger than the pipe buffer curl fails to write the
        # rest, and pipefail turns the match into a failure.
        if curl -sf "$url" 2>/dev/null | grep -F "$text" > /dev/null; then
            served=true
            break
        fi
        kill -0 "$pid" 2>/dev/null || break
        sleep 1
    done
    kill -0 "$pid" 2>/dev/null || served=false
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
    [ "$served" = true ] || fail "$url didn't serve a page with '$text' (log: $LOGS/$log.log)."
    echo "== $url served its page"
}

property() {
    "$DOTNET" msbuild "$1" -nologo "-getProperty:$2" "${@:3}" | tr -d '\r'
}

# Runs a command that must fail, with the text in its output.
expect_failure() {
    local log="$1" text="$2"
    shift 2
    echo "== $* (must fail)"
    if "$@" > "$LOGS/$log.log" 2>&1; then
        fail "'$*' succeeded, but it must fail (log: $LOGS/$log.log)."
    fi
    grep -qF -- "$text" "$LOGS/$log.log" || fail "'$*' failed without '$text' (log: $LOGS/$log.log)."
    echo "== it failed with: $text"
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
run install-template "$DOTNET" new install "$FEED/Kebechet.Watchious.Templates.$VERSION.nupkg" --debug:custom-hive "$TEMPLATE_HIVE"
new_app WatchiousApp1 "$TEMPLATE_HIVE" "$VERSION"
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
# dotnet run -f net10.0 [-- --inspector] (Kebechet.Watchious.Preview's README), on ports of their own (-- --urls=...).
preview_dll=WatchiousApp1/bin/Debug/net10.0/WatchiousApp1.dll
[ -f "$WORK_ABS/$preview_dll" ] || fail "the preview build made no $WORK/$preview_dll."
preview_app="$WORK/$preview_dll"
check_served run-preview http://localhost:5390/ '<title>Watchious preview</title>' \
    "$DOTNET" "$preview_app" --no-browser --urls=http://localhost:5390
check_served run-preview-script http://localhost:5391/preview.js 'function' \
    "$DOTNET" "$preview_app" --no-browser --urls=http://localhost:5391
check_served run-inspector http://localhost:5392/ '<title>Watchious inspector</title>' \
    "$DOTNET" "$preview_app" --inspector --no-browser --urls=http://localhost:5392

# 2. With the optional packages, at the SDK's version.
new_app WatchiousOptional "$TEMPLATE_HIVE" "$VERSION"
APP2="$WORK/WatchiousOptional/WatchiousOptional.csproj"
add_to_project "$WORK_ABS/WatchiousOptional/WatchiousOptional.csproj" '
  <ItemGroup>
    <PackageReference Include="Kebechet.Watchious.Health" Version="$(WatchiousVersion)" />
    <PackageReference Include="Kebechet.Watchious.Messaging" Version="$(WatchiousVersion)" />
  </ItemGroup>
'
# The code the packages' READMEs show.
program="$WORK_ABS/WatchiousOptional/WatchiousProgram.cs"
awk '/^using Watchious\.Hosting;/ { print "using Watchious.Health;" }
     { print }
     /^using Watchious\.Hosting;/ { print "using Watchious.Messaging;" }
     /builder\.UseWatchiousApp</ { print "        builder.Services.AddPhoneMessaging();"; print "        builder.Services.AddWatchiousHealth();" }' \
    "$program" > "$program.new"
mv "$program.new" "$program"
grep -q 'AddPhoneMessaging' "$program" && grep -q 'AddWatchiousHealth' "$program" && grep -q '^using Watchious.Health;' "$program" \
    || fail "the template's WatchiousProgram.cs no longer has the lines the READMEs' registrations go after."
cat > "$WORK_ABS/WatchiousOptional/ReadmeServices.razor" <<'EOF'
@using Watchious.Essentials
@using Watchious.Health
@using Watchious.Messaging
@inject IHaptics Haptics
@inject IHealthService Health
@inject IPhoneMessaging PhoneMessaging

<Button Text="Done" OnClick="() => Haptics.Play(HapticFeedback.Success)" />
EOF
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
    # WATCHIOUS003 (Kebechet.Watchious.Sdk's README), with this .NET for iOS made an untested version.
    untested="$("$DOTNET" msbuild "$APP2" -nologo -t:_WatchiousCheckIOSSdkVersion -p:TargetFramework=net10.0-watchos \
        -p:WatchiousTestedIOSSdkVersions=0.0.0 2>&1 | tee "$LOGS/watchious003-error.log" || true)"
    printf '%s\n' "$untested" | grep -q 'error WATCHIOUS003' || fail "an untested .NET for iOS version didn't fail with WATCHIOUS003."
    run watchious003-allowed "$DOTNET" msbuild "$APP2" -nologo -t:_WatchiousCheckIOSSdkVersion -p:TargetFramework=net10.0-watchos \
        -p:WatchiousTestedIOSSdkVersions=0.0.0 -p:WatchiousAllowUntestedIOSSdk=true
    grep -q 'warning WATCHIOUS003' "$LOGS/watchious003-allowed.log" \
        || fail "WatchiousAllowUntestedIOSSdk=true didn't turn WATCHIOUS003 into a warning."
    echo "== WATCHIOUS003: an error for an untested .NET for iOS version, a warning with WatchiousAllowUntestedIOSSdk=true"
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
# Directory.Packages.props. That file also versions the packages the SDK adds, as a solution does when another of its
# projects, such as a Razor library with the pages, references Kebechet.Watchious.Controls.
new_app WatchiousCpm "$TEMPLATE_HIVE" "$VERSION"
cat > "$WORK_ABS/WatchiousCpm/Directory.Packages.props" <<EOF
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Kebechet.Watchious.Messaging" Version="$VERSION" />
    <PackageVersion Include="Kebechet.Watchious.Controls" Version="$VERSION" />
    <PackageVersion Include="Kebechet.Watchious.Preview" Version="$VERSION" />
    <PackageVersion Include="Kebechet.Watchious.Hosts.WearOS" Version="$VERSION" />
    <PackageVersion Include="Kebechet.Watchious.Hosts.watchOS" Version="$VERSION" />
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

# 4. The SDK version: --sdk-version names another one. Installed from its source folder, the template has no version of
# its own, which only its pack stamps: its app names the token, and the restore fails (the README says to pass
# --sdk-version then).
new_app WatchiousSdkVersion "$TEMPLATE_HIVE" 9.9.9-other --sdk-version 9.9.9-other
run install-template-source "$DOTNET" new install "$(native_path "$TEMPLATE_SOURCE")" --debug:custom-hive "$TEMPLATE_SOURCE_HIVE"
new_app WatchiousFromSource "$TEMPLATE_SOURCE_HIVE" WATCHIOUS_SDK_VERSION_VALUE
echo "== dotnet restore WatchiousFromSource, which must fail"
if "$DOTNET" restore "$WORK/WatchiousFromSource/WatchiousFromSource.csproj" -nologo > "$LOGS/restore-from-source.log" 2>&1; then
    fail "an app from the template's source folder, which names no SDK version, restored."
fi
grep -q "MSB4236.*Kebechet\.Watchious\.Sdk/WATCHIOUS_SDK_VERSION_VALUE" "$LOGS/restore-from-source.log" \
    || fail "the restore of an app from the template's source folder didn't fail with MSB4236 for Kebechet.Watchious.Sdk/WATCHIOUS_SDK_VERSION_VALUE (log: $LOGS/restore-from-source.log)."
echo "== an app from the template's source folder names no SDK version, and its restore fails with MSB4236"
new_app WatchiousFromSourceVersion "$TEMPLATE_SOURCE_HIVE" "$VERSION" --sdk-version "$VERSION"
run restore-from-source-version "$DOTNET" restore "$WORK/WatchiousFromSourceVersion/WatchiousFromSourceVersion.csproj" -nologo
check_restore WatchiousFromSourceVersion "${BASE_PACKAGES[@]}"

# 5. Live mode: dotnet build -t:WatchiousLive starts a live host, which comes from the SDK package.
live_apk="$(property "$APP1" WatchiousWearOSLiveApk -p:TargetFramework=net10.0-android | tr '\\' '/')"
printf '%s' "$live_apk" | grep -qi "kebechet.watchious.sdk/$VERSION_LOWER/live/WearOS/watchious-live\.apk\$" && [ -f "$live_apk" ] \
    || fail "the Wear OS live host isn't the SDK package's: WatchiousWearOSLiveApk is '$live_apk'."
live_sources="$(property "$APP1" WatchiousWatchOSLiveDirectory -p:TargetFramework=net10.0 | tr '\\' '/')"
printf '%s' "$live_sources" | grep -qi "kebechet.watchious.sdk/$VERSION_LOWER/live/watchOS/\$" \
    && [ -f "$live_sources/LiveRuntime.swift" ] && [ -f "$live_sources/Info.plist" ] \
    || fail "the watchOS live host's sources aren't the SDK package's: WatchiousWatchOSLiveDirectory is '$live_sources'."
echo "== the live hosts are the SDK package's: $live_apk, $live_sources"
expect_failure live-preview "WatchiousLive starts the live host on a watch" \
    "$DOTNET" build "$APP1" -t:WatchiousLive -f net10.0 -p:WatchiousLivePort=5300 -nologo
grep -qF "dotnet run -f net10.0 -- --live --urls=http://localhost:5300" "$LOGS/live-preview.log" \
    || fail "-t:WatchiousLive -p:WatchiousLivePort=5300 doesn't tell to start the app on port 5300 (log: $LOGS/live-preview.log)."
echo "== it tells to start the app on its port: dotnet run -f net10.0 -- --live --urls=http://localhost:5300"
expect_failure live-watch-url "an Apple Watch can't reach the app at http://localhost:5293" \
    "$DOTNET" msbuild "$APP1" -nologo -t:_WatchiousCheckLiveWatchUrl -p:TargetFramework=net10.0-watchos -p:RuntimeIdentifier=ios-arm64
if [ "$WATCHOS" != true ]; then
    expect_failure live-wearos "adb can't reach watchious-smoke-no-device" \
        "$DOTNET" build "$APP1" -t:WatchiousLive -f net10.0-android -p:Device=watchious-smoke-no-device -nologo
    # adb's own words: the target's error alone doesn't prove that it found adb and ran it.
    grep -qF "device 'watchious-smoke-no-device' not found" "$LOGS/live-wearos.log" \
        || fail "adb didn't answer for watchious-smoke-no-device, so the target didn't find adb (log: $LOGS/live-wearos.log)."
    echo "== adb answered: device 'watchious-smoke-no-device' not found"
else
    run live-watchos "$DOTNET" build "$APP2" -t:_WatchiousBuildLiveWatchApp -f net10.0-watchos -nologo
    live_app="$(find "$WORK_ABS/WatchiousOptional/obj" -path '*net10.0-watchos*' -name WatchiousLive.app -type d 2>/dev/null | head -1)"
    [ -n "$live_app" ] || fail "the watchOS live host wasn't built (no WatchiousLive.app in obj)."
    codesign --verify "$live_app" || fail "the watchOS live host $live_app isn't signed."
    live_bundle_id="$(plutil -extract CFBundleIdentifier raw "$live_app/Info.plist")"
    live_platform="$(plutil -extract CFBundleSupportedPlatforms.0 raw "$live_app/Info.plist")"
    [ "$live_bundle_id" = com.kebechet.watchious.live ] && [ "$live_platform" = WatchSimulator ] \
        || fail "the watchOS live host is $live_bundle_id for $live_platform, not com.kebechet.watchious.live for WatchSimulator."
    echo "== watchOS live host for the simulator: $live_app"
fi

echo "== package smoke passed: $VERSION (logs in $LOGS)"
