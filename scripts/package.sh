#!/usr/bin/env bash
# Builds SafeRefinement in Release and makes the Thunderstore zip in dist/.
# Runs local only: the build needs Valheim's assemblies from your Steam install.
# The version comes from Version in Plugin.cs and is copied into manifest.json.
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION="$(grep -oP 'const string Version = "\K[^"]+' src/SafeRefinement/Plugin.cs)"
[[ -n "$VERSION" ]] || { echo "error: no Version in Plugin.cs" >&2; exit 1; }
echo "Packaging SafeRefinement $VERSION"

dotnet build src/SafeRefinement -c Release
DLL=src/SafeRefinement/bin/Release/SafeRefinement.dll

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/plugins"
cp package/thunderstore/icon.png README.md CHANGELOG.md LICENSE "$STAGE/"
cp "$DLL" "$STAGE/plugins/"
sed -E "s/(\"version_number\": \")[^\"]*\"/\1$VERSION\"/" package/thunderstore/manifest.json > "$STAGE/manifest.json"

mkdir -p dist
OUT="$PWD/dist/SafeRefinement-$VERSION.zip"
rm -f "$OUT"
(cd "$STAGE" && zip -rq "$OUT" .)
echo "Wrote $OUT"
