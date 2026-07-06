#!/bin/bash
set -euo pipefail

# Default arguments
PUBLISH_DIR=""
VERSION=""
OUTPUT_DIR="artifacts"

while [[ $# -gt 0 ]]; do
  case $1 in
    --publish-dir)
      PUBLISH_DIR="$2"
      shift 2
      ;;
    --version)
      VERSION="$2"
      shift 2
      ;;
    --output)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

if [[ -z "$PUBLISH_DIR" || -z "$VERSION" ]]; then
  echo "Usage: $0 --publish-dir <dir> --version <version> [--output <dir>]"
  exit 1
fi

mkdir -p "$OUTPUT_DIR"

APP_DIR="publish/CdpInspectorApp.app"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

# Copy binary to MacOS folder
cp -R "$PUBLISH_DIR"/* "$APP_DIR/Contents/MacOS/"

# Copy and substitute version in Info.plist
sed "s/<string>1.0.0<\/string>/<string>${VERSION}<\/string>/g" samples/CdpInspectorApp/Info.plist > "$APP_DIR/Contents/Info.plist"

# Generate icon if we are on macOS
if command -v sips &> /dev/null && command -v iconutil &> /dev/null && [ -f "icon.png" ]; then
  echo "Generating macOS application icon..."
  mkdir -p icon.iconset
  sips -z 16 16 icon.png --out icon.iconset/icon_16x16.png &> /dev/null
  sips -z 32 32 icon.png --out icon.iconset/icon_16x16@2x.png &> /dev/null
  sips -z 32 32 icon.png --out icon.iconset/icon_32x32.png &> /dev/null
  sips -z 64 64 icon.png --out icon.iconset/icon_32x32@2x.png &> /dev/null
  sips -z 128 128 icon.png --out icon.iconset/icon_128x128.png &> /dev/null
  sips -z 256 256 icon.png --out icon.iconset/icon_128x128@2x.png &> /dev/null
  sips -z 256 256 icon.png --out icon.iconset/icon_256x256.png &> /dev/null
  sips -z 512 512 icon.png --out icon.iconset/icon_256x256@2x.png &> /dev/null
  sips -z 512 512 icon.png --out icon.iconset/icon_512x512.png &> /dev/null
  cp icon.png icon.iconset/icon_512x512@2x.png
  iconutil -c icns icon.iconset
  mv icon.icns "$APP_DIR/Contents/Resources/icon.icns"
  rm -rf icon.iconset
fi

# Package DMG if on macOS
if command -v hdiutil &> /dev/null; then
  echo "Creating DMG package..."
  DMG_PATH="${OUTPUT_DIR}/cdp-inspector-osx.dmg"
  rm -f "$DMG_PATH"
  
  # Use a staging folder to guarantee .app is at the root
  STAGE_DIR="publish/dmg-stage"
  rm -rf "$STAGE_DIR"
  mkdir -p "$STAGE_DIR"
  cp -R "$APP_DIR" "$STAGE_DIR/"
  
  hdiutil create -fs HFS+ -volname "CdpInspectorApp" -srcfolder "$STAGE_DIR" "$DMG_PATH"
  rm -rf "$STAGE_DIR"
  echo "macOS DMG created: $DMG_PATH"
else
  echo "hdiutil not found. Zipping the .app bundle instead."
  cd publish
  zip -r "../${OUTPUT_DIR}/cdp-inspector-osx.zip" CdpInspectorApp.app
  cd ..
fi

rm -rf "$APP_DIR"
echo "macOS DMG build step completed."
