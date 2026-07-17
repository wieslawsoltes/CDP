#!/bin/zsh

# Ensure ports are free
echo "Cleaning up any processes on ports 9222/9223..."
PID_9222=$(lsof -t -iTCP:9222 -sTCP:LISTEN || true)
PID_9223=$(lsof -t -iTCP:9223 -sTCP:LISTEN || true)
if [ ! -z "$PID_9222" ]; then kill -9 $PID_9222 2>/dev/null || true; fi
if [ ! -z "$PID_9223" ]; then kill -9 $PID_9223 2>/dev/null || true; fi

sleep 1

# Start CdpSampleApp in background
echo "Starting CdpSampleApp..."
CDP_E2E_RUNNING=1 dotnet samples/CDP.HeadlessRunner/bin/Debug/net10.0/CDP.HeadlessRunner.dll samples/CdpSampleApp/bin/Debug/net10.0/CdpSampleApp.dll 9222 > sample_run.log 2>&1 &
SAMPLE_PID=$!

# Start CdpInspectorApp in background
echo "Starting CdpInspectorApp..."
CDP_E2E_RUNNING=1 dotnet samples/CDP.HeadlessRunner/bin/Debug/net10.0/CDP.HeadlessRunner.dll samples/CdpInspectorApp/bin/Debug/net10.0/CdpInspectorApp.dll 9223 > inspector_run.log 2>&1 &
INSPECTOR_PID=$!

# Wait for ports to open
echo "Waiting for ports to open..."
for i in {1..150}; do
  if lsof -iTCP -sTCP:LISTEN -nP | grep -q '9222' && lsof -iTCP -sTCP:LISTEN -nP | grep -q '9223'; then
    echo "Both apps started successfully."
    break
  fi
  sleep 0.1
done

# Check if started
if ! lsof -iTCP -sTCP:LISTEN -nP | grep -q '9222'; then
  echo "Error: CdpSampleApp failed to start."
  kill -9 $INSPECTOR_PID 2>/dev/null || true
  exit 1
fi
if ! lsof -iTCP -sTCP:LISTEN -nP | grep -q '9223'; then
  echo "Error: CdpInspectorApp failed to start."
  kill -9 $SAMPLE_PID 2>/dev/null || true
  exit 1
fi

# Run E2E tests
echo "Running E2E tests..."
dotnet src/CDP.Inspector.CLI/bin/Debug/net10.0/CDP.Inspector.CLI.dll -p 9223 run tests/CdpInspectorApp.E2e/simulation/popup_interaction.flow.yaml --report --video --timeout 90000
TEST_EXIT_CODE=$?

echo "Tests finished with exit code $TEST_EXIT_CODE."

# Clean up
echo "Cleaning up background processes..."
kill $SAMPLE_PID $INSPECTOR_PID 2>/dev/null || true
sleep 0.5
kill -9 $SAMPLE_PID $INSPECTOR_PID 2>/dev/null || true

exit $TEST_EXIT_CODE
