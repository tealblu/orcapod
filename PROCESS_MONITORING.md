# Process Monitoring Feature

## Overview

OrcaPod now includes automatic process monitoring that will detect when Orca Slicer (or other monitored applications) is no longer running and gracefully shut down the application.

## How It Works

The `MainService` periodically checks (default: every 30 seconds) if any of the monitored processes are still running. When none of the monitored processes are detected, OrcaPod will:

1. Log that no monitored processes are running
2. Trigger the `AllProcessesExited` event
3. Request a graceful application shutdown via the `IHostApplicationLifetime`
4. Stop all file watching and cleanup resources

## Configuration

Process monitoring is configured in the `settings.json` file:

```json
{
    "General": {
        "Interval": "00:00:30",
        "MonitorProcesses": true
    },
    "ProgramTargets": ["OrcaSlicer"]
}
```

### Configuration Options

-   **`General:Interval`**: Time interval between checks (format: `HH:MM:SS`). Default: 30 seconds
-   **`General:MonitorProcesses`**: Enable/disable process monitoring. Default: `true`
-   **`ProgramTargets`**: Array of process names to monitor (case-insensitive)

## Process Name Matching

Process names are matched case-insensitively and support partial matching. For example:

-   `"OrcaSlicer"` will match processes named `orcaslicer`, `OrcaSlicer`, or `orcaslicer-bin`
-   The exact process name depends on how the application is launched on your system

### Finding the Correct Process Name

On Linux, you can find the running process name using:

```bash
ps aux | grep -i orca
```

On Windows, use Task Manager or:

```powershell
Get-Process | Where-Object {$_.Name -like "*orca*"}
```

## Disabling Process Monitoring

To disable process monitoring and keep OrcaPod running indefinitely, set:

```json
{
    "General": {
        "MonitorProcesses": false
    }
}
```

## Platform Compatibility

This feature is fully cross-platform and works on:

-   ✅ Windows
-   ✅ Linux
-   ✅ macOS

## Logging

Process monitoring events are logged to `orcapod.log`:

-   Each check cycle logs which processes are being monitored
-   When a monitored process is found, its PID and name are logged
-   When no processes are found, a warning is logged before shutdown

## Example Log Output

```
2025-11-24 15:30:00 [INFO] Status report: ServiceName=OrcapodMainService, RunCount=1
2025-11-24 15:30:00 [INFO] Process monitoring enabled. Checking for: OrcaSlicer
2025-11-24 15:30:00 [INFO] Found running process: orcaslicer (PID: 12345)
...
2025-11-24 15:35:00 [INFO] Process monitoring enabled. Checking for: OrcaSlicer
2025-11-24 15:35:00 [WARNING] None of the monitored processes are currently running.
2025-11-24 15:35:00 [INFO] No monitored processes are running. Shutting down.
2025-11-24 15:35:00 [INFO] All monitored processes have exited. Requesting application shutdown.
2025-11-24 15:35:00 [INFO] MainService stopping
```

## Testing

To test the process monitoring feature:

1. Start OrcaPod with the `--console` flag:

    ```bash
    ./orcapod --console
    ```

2. In another terminal, start Orca Slicer (or the process you're monitoring)

3. Wait for OrcaPod to log that it found the running process

4. Close Orca Slicer

5. Within 30 seconds (or your configured interval), OrcaPod should detect the process has exited and shut down gracefully

## Troubleshooting

### OrcaPod Doesn't Quit When Process Exits

1. Check that `MonitorProcesses` is set to `true` in `settings.json`
2. Verify the process name in `ProgramTargets` matches the actual process name (use `ps` or Task Manager)
3. Check `orcapod.log` for process detection messages
4. Try using just the core part of the process name (e.g., `"orca"` instead of `"orcaslicer"`)

### OrcaPod Quits Immediately

1. Make sure the monitored process is actually running before starting OrcaPod
2. Check that the process name in `ProgramTargets` is correct
3. Look at `orcapod.log` for messages about which processes were found/not found

### False Positives

If OrcaPod keeps running even when the process has exited:

-   The process might be leaving background tasks running
-   There might be multiple instances of the process
-   Check `orcapod.log` to see which PIDs are being detected
