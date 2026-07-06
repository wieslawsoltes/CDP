#!/bin/bash
set -euo pipefail

DMG_PATH="${1:-}"

if [[ -z "$DMG_PATH" ]]; then
  echo "Usage: $0 <path-to-dmg>"
  exit 1
fi

echo "Mounting $DMG_PATH..."
hdiutil attach "$DMG_PATH" -mountpoint /Volumes/CdpInspectorApp

echo "Copying CdpInspectorApp.app to local temp directory..."
rm -rf temp-install
mkdir temp-install
cp -R /Volumes/CdpInspectorApp/CdpInspectorApp.app temp-install/

echo "Detaching DMG..."
hdiutil detach /Volumes/CdpInspectorApp

echo "Removing quarantine attributes..."
xattr -cr temp-install/CdpInspectorApp.app || true

echo "Extracting binary from bundle to run outside bundle context..."
cp temp-install/CdpInspectorApp.app/Contents/MacOS/CdpInspectorApp temp-install/CdpInspectorApp

echo "Launching CdpInspectorApp headlessly..."
./temp-install/CdpInspectorApp --headless --port 9223 &
APP_PID=$!

echo "Spawned app with PID: $APP_PID"

# Wait and poll CDP endpoint
SUCCESS=0
for i in {1..30}; do
  echo "Polling http://127.0.0.1:9223/json (attempt $i)..."
  if curl -s http://127.0.0.1:9223/json | grep -q "webSocketDebuggerUrl"; then
    echo "CDP server is active and responding!"
    SUCCESS=1
    break
  fi
  sleep 1
done

echo "Terminating CdpInspectorApp process..."
kill "$APP_PID" || true
wait "$APP_PID" 2>/dev/null || true

rm -rf temp-install

if [ $SUCCESS -ne 1 ]; then
  echo "Error: CDP server did not respond within timeout!"
  exit 1
fi

echo "macOS Installer Integration Test PASSED!"
