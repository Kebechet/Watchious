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
#     version.
# Runs in bash on Linux and macOS and in Git Bash on Windows; it needs unzip, and the android workload to evaluate the
# Android projects:
#
#   .github/scripts/check-packages.sh <package folder> <version>
#
#   DOTNET  dotnet to evaluate the projects with (default: dotnet)
set -euo pipefail
# sort and comm must order the file lists alike.
export LC_ALL=C

if [ $# -ne 2 ]; then
    echo "usage: $0 <package folder> <version>" >&2
    exit 2
fi
FEED="$1"
VERSION="$2"
DOTNET="${DOTNET:-dotnet}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
[ -d "$FEED" ] || { echo "::error::check packages: no package folder $FEED." >&2; exit 1; }

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

    case "$short" in
        Core|Essentials|Preview|Hosts.watchOS)
            expect_entries "$id" "lib/net10\.0/$name_re\.dll"
            ;;
        Controls)
            expect_entries "$id" "lib/net10\.0/$name_re\.dll" 'analyzers/dotnet/cs/Watchious\.SourceGenerators\.dll'
            ;;
        Hosts.WearOS)
            expect_entries "$id" "$ANDROID_LIB/$name_re\.dll" "$ANDROID_LIB/host-release\.aar"
            ;;
        Health|Messaging)
            module="$(printf '%s' "$short" | tr '[:upper:]' '[:lower:]')"
            expect_entries "$id" "lib/net10\.0/$name_re\.dll" "$ANDROID_LIB/$name_re\.dll" "$ANDROID_LIB/$module-release\.aar" \
                "build/$id_re\.targets" "buildTransitive/$id_re\.targets"
            expect_exactly "$id" host/watchOS/ "$(source_files "$ROOT/src/$name/watchOS" '*.swift')"
            ;;
        Sdk)
            printf '%s\n' "$nuspec" | grep -q '<packageType name="MSBuildSdk"' || error "$id isn't of package type MSBuildSdk."
            expect_exactly "$id" Sdk/ "$(source_files "$ROOT/src/$name/Sdk")"
            expect_exactly "$id" build/ "$(source_files "$ROOT/src/$name/build"; echo Watchious.Sdk.Version.props)"
            expect_exactly "$id" tools/ "$(source_files "$ROOT/src/$name/tools")"
            expect_exactly "$id" host/watchOS/ "$(source_files "$ROOT/platforms/watchOS/Host")"
            # The version the SDK adds its packages at: Version, not PackageVersion alone, sets it at pack time.
            version_props="$(unzip -p "$PACKAGE" build/Watchious.Sdk.Version.props 2>/dev/null | tr -d '\r' || true)"
            printf '%s\n' "$version_props" | grep -qF "<WatchiousVersion>$VERSION</WatchiousVersion>" \
                || error "$id: build/Watchious.Sdk.Version.props doesn't set WatchiousVersion to $VERSION."
            ;;
        Templates)
            printf '%s\n' "$nuspec" | grep -q '<packageType name="Template"' || error "$id isn't of package type Template."
            expect_exactly "$id" content/ "$(source_files "$ROOT/src/$name/content")"
            template_project="$(unzip -p "$PACKAGE" content/watchious/WatchiousApp1.csproj 2>/dev/null | tr -d '\r' || true)"
            printf '%s\n' "$template_project" | grep -q '<Project Sdk="Kebechet\.Watchious\.Sdk/' \
                || error "$id: the template's project doesn't use the Kebechet.Watchious.Sdk."
            ;;
        *)
            error "$id: this script doesn't know which files the package must have; add them here."
            ;;
    esac
    [ ${#ERRORS[@]} -ne "$errors_before" ] || echo "== $id $VERSION: ok"
done

if [ ${#ERRORS[@]} -gt 0 ]; then
    echo "== check packages: ${#ERRORS[@]} problem(s) in $FEED" >&2
    exit 1
fi
echo "== check packages passed: ${#IDS[@]} packages, version $VERSION, in $FEED"
