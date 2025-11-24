#!/bin/bash

# Test script for OrcaPod process monitoring feature
# This script helps verify that OrcaPod correctly detects when monitored processes exit

echo "==================================="
echo "OrcaPod Process Monitoring Test"
echo "==================================="
echo ""

# Check if OrcaPod binary exists
if [ ! -f "./bin/Debug/net9.0/orcapod" ]; then
    echo "ERROR: OrcaPod binary not found at ./bin/Debug/net9.0/orcapod"
    echo "Please build the project first with: dotnet build"
    exit 1
fi

# Create a test settings.json with a dummy process to monitor
echo "Creating test configuration..."
cat > ./test_settings.json << 'EOF'
{
    "General": {
        "Interval": "00:00:05",
        "MonitorProcesses": true
    },
    "Mappings": {},
    "ProgramTargets": [
        "sleep"
    ]
}
EOF

echo "Test configuration created."
echo ""
echo "Starting dummy process (sleep 20)..."
sleep 20 &
SLEEP_PID=$!
echo "Started process with PID: $SLEEP_PID"
echo ""

# Copy test settings
cp ./settings.json ./settings.json.backup
cp ./test_settings.json ./settings.json

echo "Starting OrcaPod in test mode..."
echo "OrcaPod should:"
echo "  1. Detect the 'sleep' process"
echo "  2. Run for about 20 seconds while sleep is running"
echo "  3. Exit automatically when sleep finishes"
echo ""
echo "Watch the logs in another terminal with: tail -f orcapod.log"
echo ""
echo "Press Ctrl+C to cancel the test early"
echo ""

# Start OrcaPod
./bin/Debug/net9.0/orcapod --console

# Cleanup
echo ""
echo "Restoring original settings..."
mv ./settings.json.backup ./settings.json
rm -f ./test_settings.json

# Check if sleep process is still running and kill it
if ps -p $SLEEP_PID > /dev/null 2>&1; then
    echo "Cleaning up test process..."
    kill $SLEEP_PID 2>/dev/null
fi

echo ""
echo "Test complete!"
echo "Check orcapod.log for detailed logs"
