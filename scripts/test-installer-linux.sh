#!/bin/bash
set -euo pipefail

DEB_PATH="${1:-}"

if [[ -z "$DEB_PATH" ]]; then
  echo "Usage: $0 <path-to-deb>"
  exit 1
fi

echo "Installing $DEB_PATH..."
sudo dpkg -i "$DEB_PATH"

echo "Verifying installation files..."
if [ ! -f "/usr/bin/cdp-inspector" ]; then
  echo "Error: /usr/bin/cdp-inspector not found!"
  exit 1
fi

echo "Launching cdp-inspector in headless mode..."
/usr/bin/cdp-inspector --headless --port 9223 > inspector_test.log 2>&1 &
APP_PID=$!

echo "Spawned cdp-inspector with PID: $APP_PID"

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

echo "Terminating cdp-inspector process..."
kill "$APP_PID" || true
wait "$APP_PID" 2>/dev/null || true

echo "Application log output:"
cat inspector_test.log

if [ $SUCCESS -ne 1 ]; then
  echo "Error: CDP server did not respond within timeout!"
  exit 1
fi

echo "Uninstalling package..."
sudo dpkg -r cdp-inspector

if [ -f "/usr/bin/cdp-inspector" ]; then
  echo "Error: cdp-inspector was not removed after uninstall!"
  exit 1
fi

echo "Linux Installer Integration Test PASSED!"
