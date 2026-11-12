#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_FILE="$ROOT_DIR/src/SongStash.Desktop/SongStash.Desktop.csproj"
APP_PATH="$ROOT_DIR/dist/macos-arm64/SongStash.app"
RELEASE_DIR="$ROOT_DIR/dist/releases"
VERSION="$(dotnet msbuild "$PROJECT_FILE" -getProperty:Version)"
ZIP_PATH="$RELEASE_DIR/SongStash-$VERSION-macos-arm64.zip"
EXECUTABLE="SongStash.Desktop"
ICON="SongStash.icns"
TESTER_README="$ROOT_DIR/docs/beta/README-beta.txt"

for tool in dotnet plutil codesign ditto; do
  command -v "$tool" >/dev/null || { echo "Missing required tool: $tool" >&2; exit 1; }
done

rm -rf "$ROOT_DIR/dist/macos-arm64" "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

dotnet build "$ROOT_DIR/SongStash.sln"
dotnet test "$ROOT_DIR/SongStash.sln"
"$ROOT_DIR/scripts/build-macos-app.sh"

validate_app() {
  local app="$1" plist="$1/Contents/Info.plist"
  plutil -lint "$plist"
  local actual_short actual_build
  actual_short="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$plist")"
  actual_build="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$plist")"
  for value in "$actual_short" "$actual_build"; do
    [[ -n "$value" && "$value" != *'$'* && "$value" != *'${'* ]] || { echo "Invalid bundle version metadata: '$value'" >&2; return 1; }
  done
  [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$plist")" == "app.songstash.desktop" ]]
  [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleName' "$plist")" == "SongStash" ]]
  [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleDisplayName' "$plist")" == "SongStash" ]]
  [[ "$actual_short" == "${VERSION%%-*}" ]]
  [[ "$actual_build" == "$(dotnet msbuild "$PROJECT_FILE" -getProperty:FileVersion)" ]]
  [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$plist")" == "$EXECUTABLE" ]]
  [[ "$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIconFile' "$plist")" == "$ICON" ]]
  [[ -x "$app/Contents/MacOS/$EXECUTABLE" ]]
  [[ -f "$app/Contents/Resources/$ICON" ]]
  codesign --verify --deep --strict "$app"
}

validate_app "$APP_PATH"
[[ -s "$TESTER_README" ]] || { echo "Tester README is missing or empty: $TESTER_README" >&2; exit 1; }
PACKAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/songstash-beta-package.XXXXXX")"
VERIFY_DIR="$(mktemp -d "${TMPDIR:-/tmp}/songstash-beta-verify.XXXXXX")"
trap 'rm -rf "$PACKAGE_DIR" "$VERIFY_DIR"' EXIT
ditto "$APP_PATH" "$PACKAGE_DIR/SongStash.app"
cp "$TESTER_README" "$PACKAGE_DIR/README.txt"
for file in LICENSE LICENSING.md ASSETS_LICENSE.md THIRD_PARTY_NOTICES.md; do
  [[ -s "$ROOT_DIR/$file" ]] || { echo "Required licensing file is missing or empty: $ROOT_DIR/$file" >&2; exit 1; }
  cp "$ROOT_DIR/$file" "$PACKAGE_DIR/$file"
done
for item in SongStash.app README.txt LICENSE LICENSING.md ASSETS_LICENSE.md THIRD_PARTY_NOTICES.md; do
  [[ -e "$PACKAGE_DIR/$item" ]] || { echo "$item missing from package staging directory." >&2; exit 1; }
done
(cd "$PACKAGE_DIR" && ditto -c -k --sequesterRsrc --keepParent SongStash.app "$ZIP_PATH")
(cd "$PACKAGE_DIR" && zip -q "$ZIP_PATH" README.txt LICENSE LICENSING.md ASSETS_LICENSE.md THIRD_PARTY_NOTICES.md)
unzip -t "$ZIP_PATH"
unzip -l "$ZIP_PATH"
ditto -x -k "$ZIP_PATH" "$VERIFY_DIR"
[[ -d "$VERIFY_DIR/SongStash.app" ]] || { echo "SongStash.app missing from ZIP root." >&2; exit 1; }
[[ -s "$VERIFY_DIR/README.txt" ]] || { echo "README.txt missing or empty from ZIP." >&2; exit 1; }
cmp -s "$TESTER_README" "$VERIFY_DIR/README.txt" || { echo "README.txt content does not match source." >&2; exit 1; }
for file in LICENSE LICENSING.md ASSETS_LICENSE.md THIRD_PARTY_NOTICES.md; do
  [[ -s "$VERIFY_DIR/$file" ]] || { echo "$file missing or empty from ZIP root." >&2; exit 1; }
  cmp -s "$ROOT_DIR/$file" "$VERIFY_DIR/$file" || { echo "$file content does not match source." >&2; exit 1; }
done
[[ ! -e "$VERIFY_DIR/$(basename "$PACKAGE_DIR")" ]] || { echo "Unexpected staging wrapper directory in ZIP." >&2; exit 1; }
validate_app "$VERIFY_DIR/SongStash.app"

echo "App: $APP_PATH"
echo "ZIP: $ZIP_PATH"
du -h "$ZIP_PATH"
