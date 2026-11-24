#!/usr/bin/env bash
# Packs the 10 Watchious packages (Kebechet.Watchious.*) at one version: first the Wear OS libraries with Gradle,
# whose AARs, POMs and binding jars Kebechet.Watchious.Hosts.WearOS, .Health and .Messaging build from, then each
# packable project with dotnet pack. The one way to pack, for developers and CI alike. Runs in bash on Linux and macOS
# and in Git Bash on Windows:
#
#   .github/scripts/pack.sh <version> [output folder, default artifacts/packages]
#
#   DOTNET        dotnet to pack with (default: dotnet)
#   JAVA_HOME     the JDK Gradle runs on: 17 to 21 (Gradle 8.13 can't run on JDK 25). On Windows, Android Studio's,
#                 for example "/c/Program Files/Android/openjdk/jdk-21.0.8".
#   ANDROID_HOME  the Android SDK, with platform 36, unless platforms/WearOS/local.properties sets sdk.dir
#
# The version goes in as -p:Version, so the assemblies carry it too. It also becomes PackageVersion, which the SDK writes
# into its build/Watchious.Sdk.Version.props for the packages it references and the template into its template.json for
# the SDK the app names. --version-suffix is ignored, because Directory.Build.props sets Version itself.
set -euo pipefail

if [ $# -lt 1 ] || [ $# -gt 2 ]; then
    echo "usage: $0 <version> [output folder]" >&2
    exit 2
fi
VERSION="$1"
# The version as NuGet writes it into file names (normalized, no build metadata), so the packages can be found by it.
if ! [[ "$VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "::error::pack: '$VERSION' is not a version like 1.2.3 or 1.2.3-preview.1." >&2
    exit 2
fi
DOTNET="${DOTNET:-dotnet}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="${2:-$ROOT/artifacts/packages}"
mkdir -p "$OUT"
# dotnet is a Windows program in Git Bash, so it gets the Windows form of the path (pwd -W exists only there).
if pwd -W > /dev/null 2>&1; then
    OUT="$(cd "$OUT" && pwd -W)"
else
    OUT="$(cd "$OUT" && pwd)"
fi

PROJECTS=(Core Controls Essentials Preview Hosts.watchOS Hosts.WearOS Messaging Health Sdk Templates)

# The list must match the projects that opt in to packing: a new packable project left out of it would not ship with
# the others, and nothing after this would notice, since only the listed projects are packed.
packable=()
for csproj in "$ROOT"/src/*/*.csproj; do
    grep -q '<IsPackable>true</IsPackable>' "$csproj" || continue
    name="$(basename "$csproj" .csproj)"
    packable+=("${name#Watchious.}")
done
if [ "$(printf '%s\n' "${packable[@]}" | sort)" != "$(printf '%s\n' "${PROJECTS[@]}" | sort)" ]; then
    echo "::error::pack: the packable projects in src/ (${packable[*]}) aren't the ones this script packs (${PROJECTS[*]})." >&2
    exit 1
fi

echo "== Gradle: the Wear OS libraries"
(cd "$ROOT/platforms/WearOS" && ./gradlew --console=plain :host:assembleRelease :messaging:assembleRelease :health:assembleRelease)

for project in "${PROJECTS[@]}"; do
    echo "== dotnet pack Watchious.$project $VERSION"
    "$DOTNET" pack "$ROOT/src/Watchious.$project/Watchious.$project.csproj" -c Release "-p:Version=$VERSION" -o "$OUT" -nologo
done

# Exactly the 10 packages, each under its Kebechet.Watchious.* id: a package missing, or one more (a stray package of
# this version in the folder), fails the run.
missing=()
for project in "${PROJECTS[@]}"; do
    [ -f "$OUT/Kebechet.Watchious.$project.$VERSION.nupkg" ] || missing+=("Kebechet.Watchious.$project.$VERSION.nupkg")
done
if [ ${#missing[@]} -gt 0 ]; then
    echo "::error::pack: missing in $OUT: ${missing[*]}" >&2
    exit 1
fi
packed=("$OUT"/Kebechet.Watchious.*."$VERSION".nupkg)
if [ ${#packed[@]} -ne ${#PROJECTS[@]} ]; then
    echo "::error::pack: expected ${#PROJECTS[@]} Kebechet.Watchious.*.$VERSION.nupkg in $OUT, found ${#packed[@]}:" >&2
    printf '  %s\n' "${packed[@]##*/}" >&2
    exit 1
fi
echo "== Packed ${#packed[@]} packages, version $VERSION, into $OUT:"
printf '  %s\n' "${packed[@]##*/}"
