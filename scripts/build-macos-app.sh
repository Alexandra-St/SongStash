#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_FILE="$ROOT_DIR/src/SongStash.Desktop/SongStash.Desktop.csproj"
ICON_DIR="$ROOT_DIR/Design/SongStashIcon"
OUTPUT_DIR="$ROOT_DIR/dist/macos-arm64"
APP_PATH="$OUTPUT_DIR/SongStash.app"
APP_VERSION="$(dotnet msbuild "$PROJECT_FILE" -getProperty:Version)"
APP_BUILD_VERSION="$(dotnet msbuild "$PROJECT_FILE" -getProperty:FileVersion)"
APP_SHORT_VERSION="${APP_VERSION%%-*}"

if [[ ! -f "$PROJECT_FILE" ]]; then
  echo "Desktop project not found: $PROJECT_FILE" >&2
  exit 1
fi

if [[ ! -d "$ICON_DIR" ]]; then
  echo "SongStash icon directory not found: $ICON_DIR" >&2
  exit 1
fi

# Prefer the named final master, then SongStashIcon.png. Never fall back to the
# known scaled icon exports; otherwise select the largest other PNG in the folder.
MASTER_ICON=""
MASTER_AREA=0
for preferred in "$ICON_DIR/SongStashIcon_Master.png" "$ICON_DIR/SongStashIcon.png"; do
  if [[ -f "$preferred" ]]; then
    MASTER_ICON="$preferred"
    break
  fi
done

if [[ -z "$MASTER_ICON" ]]; then
while IFS= read -r -d '' candidate; do
  name="$(basename "$candidate")"
  case "$name" in
    *_128px.png|*_64px.png|*_32px.png) continue ;;
  esac

  width="$(sips -g pixelWidth "$candidate" | awk '/pixelWidth:/ {print $2}')"
  height="$(sips -g pixelHeight "$candidate" | awk '/pixelHeight:/ {print $2}')"
  [[ "$width" =~ ^[0-9]+$ && "$height" =~ ^[0-9]+$ ]] || continue
  area=$((width * height))
  if (( area > MASTER_AREA )); then
    MASTER_ICON="$candidate"
    MASTER_AREA=$area
  fi
done < <(find "$ICON_DIR" -maxdepth 1 -type f -iname '*.png' -print0)
fi

if [[ -z "$MASTER_ICON" ]]; then
  echo "Full-size SongStash icon master PNG not found in $ICON_DIR." >&2
  echo "The directory currently contains only the scaled 128/64/32 px exports; add the final Record Box master PNG and rerun." >&2
  exit 1
fi

MASTER_WIDTH="$(sips -g pixelWidth "$MASTER_ICON" | awk '/pixelWidth:/ {print $2}')"
MASTER_HEIGHT="$(sips -g pixelHeight "$MASTER_ICON" | awk '/pixelHeight:/ {print $2}')"
if [[ ! "$MASTER_WIDTH" =~ ^[0-9]+$ || ! "$MASTER_HEIGHT" =~ ^[0-9]+$ || "$MASTER_WIDTH" -lt 512 || "$MASTER_HEIGHT" -lt 512 ]]; then
  echo "Selected icon master must be at least 512x512: $MASTER_ICON" >&2
  exit 1
fi
if [[ "$MASTER_WIDTH" != "$MASTER_HEIGHT" ]]; then
  echo "Selected icon master must be square: $MASTER_ICON (${MASTER_WIDTH}x${MASTER_HEIGHT})" >&2
  exit 1
fi
echo "Using icon master: $MASTER_ICON (${MASTER_WIDTH}x${MASTER_HEIGHT})"

for tool in dotnet sips iconutil codesign plutil; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "Required macOS packaging tool not found: $tool" >&2
    exit 1
  fi
done

WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/songstash-app-build.XXXXXX")"
trap 'rm -rf "$WORK_DIR"' EXIT
PUBLISH_DIR="$WORK_DIR/publish"
ICONSET_DIR="$WORK_DIR/SongStash.iconset"
APP_MACOS="$APP_PATH/Contents/MacOS"
APP_RESOURCES="$APP_PATH/Contents/Resources"

dotnet publish "$PROJECT_FILE" \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  -p:PublishTrimmed=false \
  -p:PublishAot=false \
  -p:UseAppHost=true

mkdir -p "$ICONSET_DIR"
sips -z 16 16 "$MASTER_ICON" --out "$ICONSET_DIR/icon_16x16.png" >/dev/null
sips -z 32 32 "$MASTER_ICON" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
sips -z 32 32 "$MASTER_ICON" --out "$ICONSET_DIR/icon_32x32.png" >/dev/null
sips -z 64 64 "$MASTER_ICON" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
sips -z 128 128 "$MASTER_ICON" --out "$ICONSET_DIR/icon_128x128.png" >/dev/null
sips -z 256 256 "$MASTER_ICON" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
sips -z 256 256 "$MASTER_ICON" --out "$ICONSET_DIR/icon_256x256.png" >/dev/null
sips -z 512 512 "$MASTER_ICON" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
sips -z 512 512 "$MASTER_ICON" --out "$ICONSET_DIR/icon_512x512.png" >/dev/null
sips -z 1024 1024 "$MASTER_ICON" --out "$ICONSET_DIR/icon_512x512@2x.png" >/dev/null
iconutil --convert icns --output "$WORK_DIR/SongStash.icns" "$ICONSET_DIR"

rm -rf "$APP_PATH"
mkdir -p "$APP_MACOS" "$APP_RESOURCES"
ditto "$PUBLISH_DIR" "$APP_MACOS"
cp "$WORK_DIR/SongStash.icns" "$APP_RESOURCES/SongStash.icns"
chmod +x "$APP_MACOS/SongStash.Desktop"

cat > "$APP_PATH/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>SongStash</string>
  <key>CFBundleDisplayName</key>
  <string>SongStash</string>
  <key>CFBundleExecutable</key>
  <string>SongStash.Desktop</string>
  <key>CFBundleIdentifier</key>
  <string>app.songstash.desktop</string>
  <key>CFBundleIconFile</key>
  <string>SongStash.icns</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleShortVersionString</key>
  <string>$APP_SHORT_VERSION</string>
  <key>CFBundleVersion</key>
  <string>$APP_BUILD_VERSION</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

plutil -lint "$APP_PATH/Contents/Info.plist"
ACTUAL_SHORT_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP_PATH/Contents/Info.plist")"
ACTUAL_BUILD_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$APP_PATH/Contents/Info.plist")"
for value in "$ACTUAL_SHORT_VERSION" "$ACTUAL_BUILD_VERSION"; do
  if [[ -z "$value" || "$value" == *'$'* || "$value" == *'${'* ]]; then
    echo "Invalid literal or empty bundle version metadata: '$value'" >&2
    exit 1
  fi
done
[[ "$ACTUAL_SHORT_VERSION" == "$APP_SHORT_VERSION" ]] || { echo "Unexpected CFBundleShortVersionString: $ACTUAL_SHORT_VERSION" >&2; exit 1; }
[[ "$ACTUAL_BUILD_VERSION" == "$APP_BUILD_VERSION" ]] || { echo "Unexpected CFBundleVersion: $ACTUAL_BUILD_VERSION" >&2; exit 1; }
codesign --force --deep --sign - "$APP_PATH"
codesign --verify --deep --strict "$APP_PATH"
echo "Built local Apple Silicon app: $APP_PATH"
