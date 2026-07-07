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

# Normalize SemVer version for Debian (e.g., 1.0.0-pr -> 1.0.0~pr)
DEB_VERSION=$(echo "$VERSION" | sed 's/-/~/g')

# Package directory structure
PKG_DIR="publish/deb-pkg"
rm -rf "$PKG_DIR"
mkdir -p "$PKG_DIR/DEBIAN"
mkdir -p "$PKG_DIR/usr/bin"
mkdir -p "$PKG_DIR/usr/share/cdp-inspector"
mkdir -p "$PKG_DIR/usr/share/applications"

# Copy published assets
cp -R "$PUBLISH_DIR"/* "$PKG_DIR/usr/share/cdp-inspector/"

# Create launcher symlink
ln -sf /usr/share/cdp-inspector/CdpInspectorApp "$PKG_DIR/usr/bin/cdp-inspector"

# Create .desktop file
cat <<EOT > "$PKG_DIR/usr/share/applications/cdp-inspector.desktop"
[Desktop Entry]
Version=1.0
Type=Application
Name=CdpInspectorApp
Comment=Chrome DevTools Protocol (CDP) client inspector for Avalonia UI applications
Exec=/usr/bin/cdp-inspector
Icon=cdp-inspector
Terminal=false
Categories=Development;Testing;
EOT

# Create control file
PKG_NAME="cdp-inspector"
cat <<EOT > "$PKG_DIR/DEBIAN/control"
Package: ${PKG_NAME}
Version: ${DEB_VERSION}
Section: devel
Priority: optional
Architecture: amd64
Maintainer: DeepMind team <antigravity@google.com>
Description: Chrome DevTools Protocol (CDP) client inspector for Avalonia UI applications
EOT

# Build package
if command -v dpkg-deb &> /dev/null; then
  dpkg-deb --root-owner-group --build "$PKG_DIR" "${OUTPUT_DIR}/${PKG_NAME}_${DEB_VERSION}_amd64.deb"
else
  echo "dpkg-deb not found. Creating package archive (tar.gz) instead for simulation."
  tar -czf "${OUTPUT_DIR}/${PKG_NAME}_${DEB_VERSION}_amd64.deb.tar.gz" -C "$PKG_DIR" .
fi

# Clean up
rm -rf "$PKG_DIR"
echo "Debian package build step completed."
