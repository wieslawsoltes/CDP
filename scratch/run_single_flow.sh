#!/bin/zsh
FLOW_FILE=$1
if [ -z "$FLOW_FILE" ]; then
  echo "Usage: $0 <flow-file>"
  exit 1
fi

echo "Cleaning up any processes on ports 9222/9223..."
PID_9222=$(lsof -t -iTCP:9222 -sTCP:LISTEN || true)
PID_9223=$(lsof -t -iTCP:9223 -sTCP:LISTEN || true)
if [ ! -z "$PID_9222" ]; then kill -9 $PID_9222 2>/dev/null || true; fi
if [ ! -z "$PID_9223" ]; then kill -9 $PID_9223 2>/dev/null || true; fi

sleep 1

echo "Starting CdpSampleApp..."
dotnet run --project samples/CDP.HeadlessRunner/CDP.HeadlessRunner.csproj -- samples/CdpSampleApp/bin/Debug/net10.0/CdpSampleApp.dll 9222 > sample_run.log 2>&1 &
SAMPLE_PID=$!

echo "Starting CdpInspectorApp..."
dotnet run --project samples/CDP.HeadlessRunner/CDP.HeadlessRunner.csproj -- samples/CdpInspectorApp/bin/Debug/net10.0/CdpInspectorApp.dll 9223 > inspector_run.log 2>&1 &
INSPECTOR_PID=$!

# Wait for ports to open
for i in {1..30}; do
  if lsof -iTCP -sTCP:LISTEN -nP | grep -q '9222' && lsof -iTCP -sTCP:LISTEN -nP | grep -q '9223'; then
    echo "Both apps started."
    break
  fi
  sleep 1
done

# Run E2E test
echo "Running E2E test: $FLOW_FILE"
dotnet run --project src/CDP.Inspector.CLI/CDP.Inspector.CLI.csproj -- -p 9223 run "$FLOW_FILE" --report
TEST_EXIT_CODE=$?

echo "Test finished with exit code $TEST_EXIT_CODE."

# Clean up
echo "Cleaning up background processes..."
kill $SAMPLE_PID $INSPECTOR_PID 2>/dev/null || true
sleep 1
kill -9 $SAMPLE_PID $INSPECTOR_PID 2>/dev/null || true

exit $TEST_EXIT_CODE
