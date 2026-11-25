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
#     version;
#   - nuget.org's page of each package: its README.md (the one in its project folder, titled with its id, with no
#     relative link, which nuget.org can't follow), the icon (the repository's icon.png), the copyright, the tags (the
#     ones in Directory.Build.props, and its own), the release notes (the GitHub release of the version) and the
#     repository commit;
#   - the license: MIT, and for a package that bundles another project's binaries, the expression this script names
#     plus a THIRD-PARTY-NOTICES.txt that names each of them: every Maven library its project downloads
#     (AndroidMavenLibrary), every Android library in it that isn't Watchious's own, and every native library inside one.
#     An MIT package must bundle none;
#   - a symbol package (.snupkg) with the .pdb of each assembly for the 8 packages with assemblies, and none for the SDK
#     and the template, which have no assembly (a symbol package of nothing fails their pack: NU5017).
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
# dotnet is a Windows program in Git Bash, so it gets the Windows form of the paths (pwd -W exists only there), which
# Git Bash itself accepts too. Its argument conversion would do the same, but MSYS_NO_PATHCONV=1 turns that off.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && { pwd -W 2> /dev/null || pwd; })"
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
    # Checked here, not by set -e: in an assignment, a failure would end the script without dotnet's message.
    if ! properties="$("$DOTNET" msbuild "$csproj" -nologo -getProperty:IsPackable -getProperty:PackageId 2>&1)"; then
        error "dotnet msbuild can't evaluate src/$name/$name.csproj: $(printf '%s' "$properties" | tr -d '\r')"
        continue
    fi
    properties="$(printf '%s\n' "$properties" | tr -d '\r')"
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
            # The version the SDK adds its packages at: Version, not PackageVersion alone, sets it at pack time.
            version_props="$(unzip -p "$PACKAGE" build/Watchious.Sdk.Version.props 2>/dev/null | tr -d '\r' || true)"
            printf '%s\n' "$version_props" | grep -qF "<WatchiousVersion>$VERSION</WatchiousVersion>" \
                || error "$id: build/Watchious.Sdk.Version.props doesn't set WatchiousVersion to $VERSION."
            symbols=false
            ;;
        Templates)
            printf '%s\n' "$nuspec" | grep -q '<packageType name="Template"' || error "$id isn't of package type Template."
            expect_exactly "$id" content/ "$(source_files "$ROOT/src/$name/content")"
            template_project="$(unzip -p "$PACKAGE" content/watchious/WatchiousApp1.csproj 2>/dev/null | tr -d '\r' || true)"
            printf '%s\n' "$template_project" | grep -q '<Project Sdk="Kebechet\.Watchious\.Sdk/' \
                || error "$id: the template's project doesn't use the Kebechet.Watchious.Sdk."
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
    bundled="$(maven_libraries "$ROOT/src/$name/$name.csproj"; bundled_binaries)"
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
