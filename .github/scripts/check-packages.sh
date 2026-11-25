#!/usr/bin/env bash
# Checks the packed Kebechet.Watchious.* packages before anything installs or publishes them. A package id is permanent
# on nuget.org, and the traps here pass every in-repo build, which never uses the packages:
#   - every packable project in src/ has the id Kebechet.<project name>, and there are exactly 10;
#   - the folder holds exactly those 10 packages at the version, each nuspec with its id and the version, and no
#     file path with an empty folder (host/watchOS//file) or a backslash;
#   - no nuspec depends on an old Watchious.* id, and the dependencies on the other packages are at the version;
#   - a props or targets file in build/ or buildTransitive/ is named after its package, the only name NuGet imports
#     (Health's and Messaging's put their Swift into the watchOS host). The SDK is the exception: Sdk/Sdk.props and
#     Sdk.targets import its build/ files by path;
#   - each package has its files: the assemblies, the Android libraries, the analyzer, the Swift, and for the SDK and the
#     template every file of the source folders they pack, plus the SDK's build/Watchious.Sdk.Version.props with the
#     version and its live hosts (dotnet build -t:WatchiousLive): the watchOS one's sources, and the Wear OS one,
#     live/WearOS/watchious-live.apk, whose signature is valid and made with the repository's live key
#     (platforms/WearOS/live/watchious-live.keystore: adb updates an installed app only with the same signature), whose
#     versionName is the version, and which kept the Rounded Material icons that MaterialIcon looks up by name;
#   - the template's app names the SDK by the token WATCHIOUS_SDK_VERSION, and its template.json is the source one with
#     the version as the default that replaces it; no WATCHIOUS_SDK_VERSION_VALUE is left anywhere in the package;
#   - nuget.org's page of each package: its README.md (the one in its project folder, titled with its id, with no
#     relative link, which nuget.org can't follow), the icon (the repository's icon.png), the copyright, the tags (the
#     ones in Directory.Build.props, and its own), the release notes (the GitHub release of the version) and the
#     repository commit;
#   - the license: MIT, and for a package that bundles another project's binaries, the expression this script names
#     plus a THIRD-PARTY-NOTICES.txt that names each of them: every Maven library its project downloads
#     (AndroidMavenLibrary), every Android library in it that isn't Watchious's own, every native library inside one,
#     and in an Android app (the SDK's live host) every AndroidX and kotlinx library at the version the app records, and
#     the Kotlin standard library. An MIT package must bundle none;
#   - a symbol package (.snupkg) with the .pdb of each assembly for the 8 packages with assemblies, and none for the SDK
#     and the template, which have no assembly (a symbol package of nothing fails their pack: NU5017).
# Runs in bash on Linux and macOS and in Git Bash on Windows; it needs unzip, the android workload to evaluate the
# Android projects, a JDK (keytool, and java for apksigner) and the Android SDK's build tools (apksigner, aapt2):
#
#   .github/scripts/check-packages.sh <package folder> <version> [work folder, default a new temp folder]
#
#   DOTNET        dotnet to evaluate the projects with (default: dotnet)
#   JAVA_HOME     the JDK, unless java and keytool are on the PATH
#   ANDROID_HOME  the Android SDK (or ANDROID_SDK_ROOT), unless platforms/WearOS/local.properties sets sdk.dir; its
#                 newest build tools are used
# The work folder gets the live host's APK, unpacked from the SDK package, for apksigner and aapt2.
set -euo pipefail
# sort and comm must order the file lists alike.
export LC_ALL=C

if [ $# -lt 2 ] || [ $# -gt 3 ]; then
    echo "usage: $0 <package folder> <version> [work folder]" >&2
    exit 2
fi
FEED="$1"
VERSION="$2"
WORK="${3:-}"
DOTNET="${DOTNET:-dotnet}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
[ -d "$FEED" ] || { echo "::error::check packages: no package folder $FEED." >&2; exit 1; }
[ -n "$WORK" ] || WORK="$(mktemp -d "${TMPDIR:-/tmp}/watchious-check-packages.XXXXXX")"
mkdir -p "$WORK"

ERRORS=()
# A list of lines as one line, for a message.
words() {
    printf '%s\n' "$1" | sed '/^$/d' | tr '\n' ' ' | sed 's/ $//'
}
error() {
    ERRORS+=("$*")
    echo "::error::check packages: $*" >&2
}

IDS=()
for csproj in "$ROOT"/src/*/*.csproj; do
    name="$(basename "$csproj" .csproj)"
    properties="$("$DOTNET" msbuild "$csproj" -nologo -getProperty:IsPackable -getProperty:PackageId | tr -d '\r')"
    is_packable="$(printf '%s\n' "$properties" | sed -n 's/.*"IsPackable": *"\([^"]*\)".*/\1/p')"
    package_id="$(printf '%s\n' "$properties" | sed -n 's/.*"PackageId": *"\([^"]*\)".*/\1/p')"
    [ "$is_packable" = "true" ] || continue
    if [ "$package_id" != "Kebechet.$name" ]; then
        error "src/$name/$name.csproj packs as '$package_id', expected Kebechet.$name."
    fi
    IDS+=("Kebechet.$name")
done
if [ ${#IDS[@]} -eq 0 ]; then
    error "no packable project in src/."
    exit 1
fi
if [ ${#IDS[@]} -ne 10 ]; then
    error "expected 10 packable projects in src/, found ${#IDS[@]}: ${IDS[*]}."
fi
[ ${#ERRORS[@]} -ne 0 ] || echo "== ${#IDS[@]} packable projects, each with the id Kebechet.<project name>: ok"

expected_files="$(for id in "${IDS[@]}"; do echo "$id.$VERSION.nupkg"; done | sort)"
found_files="$(cd "$FEED" && for file in *."$VERSION".nupkg; do [ ! -e "$file" ] || echo "$file"; done | sort)"
missing_files="$(comm -23 <(printf '%s\n' "$expected_files") <(printf '%s\n' "$found_files" | sed '/^$/d'))"
extra_files="$(comm -13 <(printf '%s\n' "$expected_files") <(printf '%s\n' "$found_files" | sed '/^$/d'))"
[ -z "$missing_files" ] || error "$FEED lacks $(words "$missing_files")."
[ -z "$extra_files" ] || error "$FEED has packages of $VERSION besides the 10: $(words "$extra_files")."
# Which ids have a symbol package is checked per package below; this catches one of another id.
symbol_files="$(cd "$FEED" && for file in *."$VERSION".snupkg; do [ ! -e "$file" ] || echo "${file%.snupkg}.nupkg"; done | sort)"
stray_symbol_files="$(comm -13 <(printf '%s\n' "$expected_files") <(printf '%s\n' "$symbol_files" | sed '/^$/d'))"
[ -z "$stray_symbol_files" ] || error "$FEED has symbol packages of $VERSION besides the 10 packages': $(words "$stray_symbol_files" | sed 's/\.nupkg/.snupkg/g')."

SHARED_TAGS="$(sed -n 's|.*<PackageTags>\([^$<]*\)</PackageTags>.*|\1|p' "$ROOT/Directory.Build.props" | tr ';' '\n' | sed '/^$/d' | sort -u)"
[ -n "$SHARED_TAGS" ] || error "Directory.Build.props sets no PackageTags shared by the packages."
ICON="$ROOT/icon.png"
[ -f "$ICON" ] || error "the repository has no icon.png."
# Watchious's own Android libraries: the Gradle modules' release AARs.
GRADLE_MODULES="$(sed -n 's/^include(":\([^"]*\)")/\1/p' "$ROOT/platforms/WearOS/settings.gradle.kts")"

# Every file in the package under a folder (prefix ends with /), relative to it.
package_entries() {
    unzip -Z1 "$1" | tr -d '\r' | sed -n "s|^$2||p" | sort
}

# The files a pack glob such as Include="build\**" (or "watchOS\*.swift", given the name pattern) takes from a source
# folder, relative to it.
source_files() {
    (cd "$1" && find . -type f -name "${2:-*}" -not -path '*/bin/*' -not -path '*/obj/*' | sed 's|^\./||' | sort)
}

# The package's files under a prefix are exactly the given list (one per line).
expect_exactly() {
    local id="$1" prefix="$2" expected="$3" actual missing extra
    actual="$(package_entries "$PACKAGE" "$prefix")"
    missing="$(comm -23 <(printf '%s\n' "$expected" | sed '/^$/d' | sort) <(printf '%s\n' "$actual" | sed '/^$/d'))"
    extra="$(comm -13 <(printf '%s\n' "$expected" | sed '/^$/d' | sort) <(printf '%s\n' "$actual" | sed '/^$/d'))"
    [ -z "$missing" ] || error "$id lacks in $prefix: $(words "$missing")"
    [ -z "$extra" ] || error "$id has in $prefix files its source doesn't: $(words "$extra")"
}

# Text to match literally in an extended regular expression.
regex_escape() {
    printf '%s' "$1" | sed 's/[].*^()+?{}|$[]/\\&/g'
}

# The package has a file matching each extended regular expression.
expect_entries() {
    local id="$1" pattern
    shift
    for pattern in "$@"; do
        printf '%s\n' "$ENTRIES" | grep -qE "^$pattern\$" || error "$id has no file matching $pattern."
    done
}

# The project's AndroidMavenLibrary items, as group:artifact:version: the Java libraries its Android build downloads
# from a Maven repository and packs, unbound.
maven_libraries() {
    local csproj="$1" android
    android="$("$DOTNET" msbuild "$csproj" -nologo -getProperty:TargetFrameworks -getProperty:TargetFramework | tr -d '\r' \
        | sed -n 's/.*"TargetFrameworks\{0,1\}": *"\([^"]*\)".*/\1/p' | tr ';' '\n' | grep -E -- '-android' | head -1 || true)"
    [ -n "$android" ] || return 0
    "$DOTNET" msbuild "$csproj" -nologo -getItem:AndroidMavenLibrary "-p:TargetFramework=$android" | tr -d '\r' \
        | grep -oE '"(Identity|Version)": *"[^"]*"' \
        | awk -F'"' '$2 == "Identity" { id = $4 } $2 == "Version" { print id ":" $4 }'
}

# What the package bundles of other projects, one per line: every Android library (.aar) in lib/ that isn't Watchious's
# own (a Gradle module's release AAR, or the one .NET for Android makes of the project), and every native library
# inside any of its Android libraries.
bundled_binaries() {
    local aar file module own
    for aar in $(printf '%s\n' "$ENTRIES" | grep -E '^lib/.*\.aar$' || true); do
        file="${aar##*/}"
        own=false
        [ "$file" != "$name.aar" ] || own=true
        for module in $GRADLE_MODULES; do
            [ "$file" != "$module-release.aar" ] || own=true
        done
        [ "$own" = true ] || echo "$file"
        # unzip can't list an archive it reads from a pipe, but an archive stores its entry names uncompressed, so they
        # are read from its bytes, with no copy of the AAR on disk.
        unzip -p "$PACKAGE" "$aar" | grep -aoE 'jni/[^/[:cntrl:]]+/[^/[:cntrl:]]+\.so' | sed 's|.*/||' || true
    done | sort -u
}

# For an error about a native library inside the AAR .NET for Android makes of the project: .NET for Android 36.1.69
# (workload set 10.0.401) on Windows copies a dependency's native libraries into it, CI's (10.0.404) doesn't.
native_hint() {
    if printf '%s\n' "$1" | grep -q '\.so$'; then
        echo " A native library inside $name.aar can come from the .NET for Android that packed it: pack with the workload set CI uses."
    fi
}

# What an Android app in the package (the SDK's live host) bundles, one per line as its THIRD-PARTY-NOTICES.txt names
# it: each AndroidX library at the version it records in the app (META-INF/<group>_<artifact>.version; only its id when
# that file holds no version, as androidx.arch.core:core-runtime's), kotlinx.coroutines likewise
# (META-INF/kotlinx_coroutines_<module>.version), the Kotlin standard library and each native library.
apk_libraries() {
    local apk file library version entries
    for apk in $(printf '%s\n' "$ENTRIES" | grep -E '\.apk$' || true); do
        unzip -o -q "$PACKAGE" "$apk" -d "$WORK/$id"
        entries="$(unzip -Z1 "$WORK/$id/$apk" | tr -d '\r')"
        for file in $(printf '%s\n' "$entries" | grep -E '^META-INF/(androidx\.[^/]+|kotlinx_coroutines_[^/]+)\.version$' || true); do
            library="${file#META-INF/}"
            library="${library%.version}"
            case "$library" in
                kotlinx_coroutines_*) library="org.jetbrains.kotlinx:kotlinx-coroutines-${library#kotlinx_coroutines_}" ;;
                *) library="${library%%_*}:${library#*_}" ;;
            esac
            version="$(unzip -p "$WORK/$id/$apk" "$file" | tr -d '\r\n')"
            if [[ "$version" =~ ^[0-9][0-9A-Za-z.+-]*$ ]]; then
                echo "$library:$version"
            else
                echo "$library:"
            fi
        done
        if printf '%s\n' "$entries" | grep -qxF kotlin/kotlin.kotlin_builtins; then
            echo "org.jetbrains.kotlin:kotlin-stdlib:"
        fi
        printf '%s\n' "$entries" | grep -E '^lib/[^/]+/[^/]+\.so$' | sed 's|.*/||' || true
    done | sort -u
}

# The newest build tools of the Android SDK, for apksigner and aapt2.
android_build_tools() {
    local sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}" properties="$ROOT/platforms/WearOS/local.properties" version
    if [ -z "$sdk" ] && [ -f "$properties" ]; then
        # local.properties escapes : and \ (sdk.dir=C\:\\Users\\...).
        sdk="$(sed -n 's/^sdk\.dir=//p' "$properties" | tr -d '\r' | sed 's/\\\(.\)/\1/g')"
    fi
    if [ -n "$sdk" ] && [ -d "$sdk/build-tools" ]; then
        version="$(ls "$sdk/build-tools" | sort -V | tail -1)"
        [ -z "$version" ] || printf '%s\n' "$sdk/build-tools/$version"
    fi
}
BUILD_TOOLS="$(android_build_tools)"

# A build tool by its name: on Windows apksigner is a .bat file and aapt2 an .exe.
build_tool() {
    local candidate
    [ -n "$BUILD_TOOLS" ] || return 0
    for candidate in "$BUILD_TOOLS/$1" "$BUILD_TOOLS/$1.bat" "$BUILD_TOOLS/$1.exe"; do
        if [ -f "$candidate" ]; then
            printf '%s\n' "$candidate"
            return 0
        fi
    done
}

KEYTOOL=keytool
[ -z "${JAVA_HOME:-}" ] || KEYTOOL="$JAVA_HOME/bin/keytool"

# The SHA-256 digest of the live key's certificate, in lowercase hex. The keystore's password and alias are the ones
# platforms/WearOS/live/build.gradle.kts signs with.
live_key_digest() {
    local gradle="$ROOT/platforms/WearOS/live/build.gradle.kts" password alias
    password="$(sed -n 's/.*storePassword = "\([^"]*\)".*/\1/p' "$gradle")"
    alias="$(sed -n 's/.*keyAlias = "\([^"]*\)".*/\1/p' "$gradle")"
    "$KEYTOOL" -list -v -keystore "$ROOT/platforms/WearOS/live/watchious-live.keystore" -storetype PKCS12 \
        -storepass "$password" -alias "$alias" 2>/dev/null | tr -d '\r' \
        | sed -n 's/^[[:space:]]*SHA256: //p' | tr -d ':' | tr '[:upper:]' '[:lower:]' || true
}

# The SDK's Wear OS live host: a valid signature made with the live key, the version as its versionName, and the Rounded
# Material icons kept, which MaterialIcon looks up by name (two that no code of the host names stand for them all).
check_live_apk() {
    local apk="$WORK/$id/live/WearOS/watchious-live.apk" apksigner aapt2 signature digest expected version_name icons
    if ! unzip -o -q "$PACKAGE" live/WearOS/watchious-live.apk -d "$WORK/$id"; then
        error "$id has no live/WearOS/watchious-live.apk."
        return 0
    fi
    apksigner="$(build_tool apksigner)"
    aapt2="$(build_tool aapt2)"
    if [ -z "$apksigner" ] || [ -z "$aapt2" ]; then
        error "$id: no apksigner and aapt2 to check its live host with: set ANDROID_HOME to an Android SDK with build tools."
        return 0
    fi
    if ! signature="$("$apksigner" verify --print-certs "$apk" 2>&1 | tr -d '\r')"; then
        error "$id: live/WearOS/watchious-live.apk has no valid signature: $(words "$signature")"
        return 0
    fi
    digest="$(printf '%s\n' "$signature" | grep -oiE 'certificate SHA-256 digest: [0-9a-f]+' | sed 's/.*: //' | tr '[:upper:]' '[:lower:]' | sort -u || true)"
    expected="$(live_key_digest)"
    if [ -z "$expected" ]; then
        error "$id: the live key's certificate couldn't be read from platforms/WearOS/live/watchious-live.keystore (keytool: $KEYTOOL)."
    elif [ "$digest" != "$expected" ]; then
        error "$id: live/WearOS/watchious-live.apk is signed with the certificate $(words "$digest"), not with the live key's $expected (platforms/WearOS/live/watchious-live.keystore)."
    fi
    # || true: under pipefail a failing aapt2, or a grep that finds no icon, would end the script here, before the
    # error that says what broke.
    version_name="$("$aapt2" dump badging "$apk" | tr -d '\r' | sed -n "s/^package: .* versionName='\([^']*\)'.*/\1/p" || true)"
    [ "$version_name" = "$VERSION" ] || error "$id: the live host's versionName is '$version_name', not $VERSION."
    icons="$(unzip -p "$apk" 'classes*.dex' | grep -aoE 'Landroidx/compose/material/icons/(rounded/Hiking|automirrored/rounded/Article)Kt;' | sort -u | wc -l | tr -d ' ' || true)"
    [ "$icons" = 2 ] || error "$id: R8 dropped Rounded Material icons from the live host (Icons.Rounded.Hiking, Icons.AutoMirrored.Rounded.Article): check platforms/WearOS/live/proguard-rules.pro."
}

ANDROID_LIB='lib/net10\.0-android[0-9.]+'
BACKSLASH=$'\\'

for id in "${IDS[@]}"; do
    PACKAGE="$FEED/$id.$VERSION.nupkg"
    [ -f "$PACKAGE" ] || continue
    errors_before=${#ERRORS[@]}
    ENTRIES="$(unzip -Z1 "$PACKAGE" | tr -d '\r')"
    name="${id#Kebechet.}"
    short="${name#Watchious.}"
    id_re="$(regex_escape "$id")"
    name_re="$(regex_escape "$name")"

    nuspec="$(unzip -p "$PACKAGE" "$id.nuspec" 2>/dev/null | tr -d '\r' || true)"
    if [ -z "$nuspec" ]; then
        error "$id has no $id.nuspec."
        continue
    fi
    # Packed on Linux, a PackagePath such as host\watchOS\ gives entries like host/watchOS//file.
    malformed="$(printf '%s\n' "$ENTRIES" | grep -F -e '//' -e "$BACKSLASH" || true)"
    [ -z "$malformed" ] || error "$id has entries with an empty folder or a backslash in their path: $(words "$malformed")"
    printf '%s\n' "$nuspec" | grep -qF "<id>$id</id>" || error "$id: its nuspec doesn't say <id>$id</id>."
    printf '%s\n' "$nuspec" | grep -qF "<version>$VERSION</version>" || error "$id: its nuspec doesn't say <version>$VERSION</version>."

    while IFS= read -r dependency; do
        [ -n "$dependency" ] || continue
        dependency_id="$(printf '%s' "$dependency" | sed -n 's/.* id="\([^"]*\)".*/\1/p')"
        dependency_version="$(printf '%s' "$dependency" | sed -n 's/.* version="\([^"]*\)".*/\1/p')"
        case "$dependency_id" in
            Watchious.*) error "$id depends on the old id $dependency_id." ;;
            Kebechet.Watchious.*)
                printf '%s\n' "${IDS[@]}" | grep -qxF "$dependency_id" || error "$id depends on $dependency_id, which isn't one of the 10 packages."
                [ "$dependency_version" = "$VERSION" ] || [ "$dependency_version" = "[$VERSION]" ] \
                    || error "$id depends on $dependency_id $dependency_version, not $VERSION."
                ;;
        esac
    done <<< "$(printf '%s\n' "$nuspec" | grep -oE '<dependency [^>]*>' || true)"

    # NuGet imports build/<id>.props|targets and buildTransitive/<id>.props|targets only: any other name is never
    # imported. The SDK is resolved as an MSBuild SDK, never as a PackageReference, and imports its build/ by path.
    if [ "$short" != "Sdk" ]; then
        unimported="$(printf '%s\n' "$ENTRIES" | grep -E '^(build|buildTransitive)/[^/]+\.(props|targets)$' \
            | grep -vE "^(build|buildTransitive)/$id_re\.(props|targets)\$" || true)"
        [ -z "$unimported" ] || error "$id has MSBuild files NuGet never imports, since they aren't named $id.props or .targets: $(words "$unimported")"
    fi

    # MIT is Watchious's own license; a package that bundles another project's binaries is under theirs too.
    license=MIT
    symbols=true
    case "$short" in
        Core|Essentials|Preview|Hosts.watchOS)
            expect_entries "$id" "lib/net10\.0/$name_re\.dll"
            ;;
        Controls)
            expect_entries "$id" "lib/net10\.0/$name_re\.dll" 'analyzers/dotnet/cs/Watchious\.SourceGenerators\.dll'
            ;;
        Hosts.WearOS)
            expect_entries "$id" "$ANDROID_LIB/$name_re\.dll" "$ANDROID_LIB/host-release\.aar"
            # Wear Compose Material 3.
            license='MIT AND Apache-2.0'
            ;;
        Health|Messaging)
            module="$(printf '%s' "$short" | tr '[:upper:]' '[:lower:]')"
            expect_entries "$id" "lib/net10\.0/$name_re\.dll" "$ANDROID_LIB/$name_re\.dll" "$ANDROID_LIB/$module-release\.aar" \
                "build/$id_re\.targets" "buildTransitive/$id_re\.targets"
            expect_exactly "$id" host/watchOS/ "$(source_files "$ROOT/src/$name/watchOS" '*.swift')"
            # Health Services and its protos, and the protobuf runtime it repackages.
            [ "$short" != Health ] || license='MIT AND Apache-2.0 AND BSD-3-Clause'
            ;;
        Sdk)
            printf '%s\n' "$nuspec" | grep -q '<packageType name="MSBuildSdk"' || error "$id isn't of package type MSBuildSdk."
            expect_exactly "$id" Sdk/ "$(source_files "$ROOT/src/$name/Sdk")"
            expect_exactly "$id" build/ "$(source_files "$ROOT/src/$name/build"; echo Watchious.Sdk.Version.props)"
            expect_exactly "$id" tools/ "$(source_files "$ROOT/src/$name/tools")"
            expect_exactly "$id" host/watchOS/ "$(source_files "$ROOT/platforms/watchOS/Host")"
            expect_exactly "$id" live/watchOS/ "$(source_files "$ROOT/platforms/watchOS/Live")"
            expect_exactly "$id" live/WearOS/ watchious-live.apk
            check_live_apk
            # The Wear OS live host's AndroidX, Kotlin and other libraries.
            license='MIT AND Apache-2.0'
            # The version the SDK adds its packages at, its own, written at pack time.
            version_props="$(unzip -p "$PACKAGE" build/Watchious.Sdk.Version.props 2>/dev/null | tr -d '\r' || true)"
            printf '%s\n' "$version_props" | grep -qF "<WatchiousVersion>$VERSION</WatchiousVersion>" \
                || error "$id: build/Watchious.Sdk.Version.props doesn't set WatchiousVersion to $VERSION."
            symbols=false
            ;;
        Templates)
            printf '%s\n' "$nuspec" | grep -q '<packageType name="Template"' || error "$id isn't of package type Template."
            expect_exactly "$id" content/ "$(source_files "$ROOT/src/$name/content")"
            template_project="$(unzip -p "$PACKAGE" content/watchious/WatchiousApp1.csproj 2>/dev/null | tr -d '\r' | head -1 || true)"
            [ "$template_project" = '<Project Sdk="Kebechet.Watchious.Sdk/WATCHIOUS_SDK_VERSION">' ] \
                || error "$id: the template's project starts with '$template_project', not <Project Sdk=\"Kebechet.Watchious.Sdk/WATCHIOUS_SDK_VERSION\">, the token its sdkVersion parameter replaces."
            # The pack stamps the package version as the default of the template's sdkVersion parameter
            # (https://github.com/Kebechet/Watchious/issues/12), and changes nothing else in template.json.
            template_json="$(unzip -p "$PACKAGE" content/watchious/.template.config/template.json 2>/dev/null | tr -d '\r' || true)"
            sdk_version_default="$(printf '%s\n' "$template_json" | awk '
                /"sdkVersion": *\{/ { in_symbol = 1; next }
                in_symbol && /^ *\},? *$/ { exit }
                in_symbol && /"defaultValue"/ { sub(/.*"defaultValue": *"/, ""); sub(/".*/, ""); print; exit }')"
            [ "$sdk_version_default" = "$VERSION" ] \
                || error "$id: the default of the template's sdkVersion parameter is '$sdk_version_default', not the package version $VERSION."
            [ "$template_json" = "$(sed "s/\"WATCHIOUS_SDK_VERSION_VALUE\"/\"$VERSION\"/" "$ROOT/src/$name/content/watchious/.template.config/template.json" | tr -d '\r')" ] \
                || error "$id: its template.json isn't the source one with the default WATCHIOUS_SDK_VERSION_VALUE set to $VERSION."
            unstamped="$(unzip -c "$PACKAGE" | tr -d '\r' | awk '
                /^ *(inflating|extracting): / { file = $0; sub(/^ *(inflating|extracting): /, "", file); sub(/ +$/, "", file); next }
                /WATCHIOUS_SDK_VERSION_VALUE/ { print file }' | sort -u)"
            [ -z "$unstamped" ] || error "$id has the token WATCHIOUS_SDK_VERSION_VALUE, which the pack replaces with the version, in $(words "$unstamped")"
            symbols=false
            ;;
        *)
            error "$id: this script doesn't know which files the package must have; add them here."
            ;;
    esac

    printf '%s\n' "$nuspec" | grep -qF '<readme>README.md</readme>' || error "$id: its nuspec has no <readme>README.md</readme>."
    readme_source="$ROOT/src/$name/README.md"
    if ! printf '%s\n' "$ENTRIES" | grep -qxF README.md; then
        error "$id has no README.md."
    elif [ ! -f "$readme_source" ]; then
        error "$id: src/$name/README.md doesn't exist."
    else
        unzip -p "$PACKAGE" README.md | cmp -s - "$readme_source" || error "$id: its README.md isn't src/$name/README.md."
        [ "$(head -1 "$readme_source" | tr -d '\r')" = "# $id" ] || error "$id: src/$name/README.md doesn't start with '# $id'."
        relative_links="$(grep -oE '\]\([^)]*\)' "$readme_source" | grep -vE '^\]\((https?://|#)' || true)"
        [ -z "$relative_links" ] || error "$id: src/$name/README.md has links relative to the repository, which nuget.org can't follow: $(words "$relative_links")"
    fi
    printf '%s\n' "$nuspec" | grep -qF '<icon>icon.png</icon>' || error "$id: its nuspec has no <icon>icon.png</icon>."
    if ! printf '%s\n' "$ENTRIES" | grep -qxF icon.png; then
        error "$id has no icon.png."
    elif [ -f "$ICON" ]; then
        unzip -p "$PACKAGE" icon.png | cmp -s - "$ICON" || error "$id: its icon.png isn't the repository's icon.png."
    fi
    printf '%s\n' "$nuspec" | grep -qE '<copyright>Copyright \(c\) [0-9]{4} Kebechet</copyright>' \
        || error "$id: its nuspec has no <copyright> of Kebechet."
    tags="$(printf '%s\n' "$nuspec" | sed -n 's|.*<tags>\(.*\)</tags>.*|\1|p' | tr ' ' '\n' | sed '/^$/d' | sort -u)"
    missing_tags="$(comm -23 <(printf '%s\n' "$SHARED_TAGS") <(printf '%s\n' "$tags"))"
    [ -z "$missing_tags" ] || error "$id lacks tags that every package has (Directory.Build.props): $(words "$missing_tags")"
    [ -n "$(comm -13 <(printf '%s\n' "$SHARED_TAGS") <(printf '%s\n' "$tags") | sed '/^$/d')" ] \
        || error "$id has no tags of its own besides the shared ones; add them to its PackageTags."
    release_notes="https://github.com/Kebechet/Watchious/releases/tag/v$VERSION"
    printf '%s\n' "$nuspec" | grep -qF "<releaseNotes>$release_notes</releaseNotes>" || error "$id: its release notes aren't $release_notes."
    printf '%s\n' "$nuspec" | grep -qE '<repository type="git" url="https://github.com/Kebechet/Watchious"[^>]* commit="[0-9a-f]{40}"' \
        || error "$id: its nuspec has no <repository> with the commit."

    printf '%s\n' "$nuspec" | grep -qF "<license type=\"expression\">$license</license>" \
        || error "$id: its license isn't the expression '$license': $(printf '%s\n' "$nuspec" | grep -oE '<license[^<]*</license>' || echo none)."
    bundled="$(maven_libraries "$ROOT/src/$name/$name.csproj"; bundled_binaries; apk_libraries)"
    notices="$(unzip -p "$PACKAGE" THIRD-PARTY-NOTICES.txt 2>/dev/null | tr -d '\r' || true)"
    if [ "$license" = MIT ]; then
        [ -z "$bundled" ] || error "$id bundles other projects' binaries but its license is MIT alone: $(words "$bundled"). Name them and their licenses in a THIRD-PARTY-NOTICES.txt, and set the expression in the project and here.$(native_hint "$bundled")"
        [ -z "$notices" ] || error "$id has a THIRD-PARTY-NOTICES.txt, but its license is MIT alone."
    elif [ -z "$notices" ]; then
        error "$id: its license is '$license', but it has no THIRD-PARTY-NOTICES.txt."
    else
        unnamed="$(printf '%s\n' "$bundled" | sed '/^$/d' | while IFS= read -r binary; do
            printf '%s\n' "$notices" | grep -qF "$binary" || echo "$binary"
        done)"
        [ -z "$unnamed" ] || error "$id: its THIRD-PARTY-NOTICES.txt doesn't name what it bundles: $(words "$unnamed").$(native_hint "$unnamed")"
    fi

    symbol_package="$FEED/$id.$VERSION.snupkg"
    if [ "$symbols" = true ]; then
        if [ ! -f "$symbol_package" ]; then
            error "$id has no symbol package $id.$VERSION.snupkg."
        else
            expected_pdbs="$(printf '%s\n' "$ENTRIES" | grep -E '^lib/.+\.dll$' | sed 's/\.dll$/.pdb/' || true)"
            [ -n "$expected_pdbs" ] || error "$id has no assembly in lib/."
            missing_pdbs="$(comm -23 <(printf '%s\n' "$expected_pdbs" | sed '/^$/d' | sort) <(unzip -Z1 "$symbol_package" | tr -d '\r' | sort))"
            [ -z "$missing_pdbs" ] || error "$id.$VERSION.snupkg lacks $(words "$missing_pdbs")"
        fi
    elif [ -f "$symbol_package" ]; then
        error "$id has no assembly, but a symbol package $id.$VERSION.snupkg."
    fi
    [ ${#ERRORS[@]} -ne "$errors_before" ] || echo "== $id $VERSION: ok"
done

if [ ${#ERRORS[@]} -gt 0 ]; then
    echo "== check packages: ${#ERRORS[@]} problem(s) in $FEED" >&2
    exit 1
fi
echo "== check packages passed: ${#IDS[@]} packages, version $VERSION, in $FEED"
