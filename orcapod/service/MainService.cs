using System;
using System.Linq;
using System.Threading;
using Timer = System.Threading.Timer;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OrcaPod.Utils;
using Orcapod.Utils;

namespace OrcaPod.Service
{
    // Cross-platform core service (no Windows-only APIs)
    public class MainService : IDisposable
    {
        // Remove injected logger, use LogHandler
        private Timer? _timer;
        private TimeSpan _interval = TimeSpan.FromSeconds(30);
        private int _running;
        private CancellationTokenSource? _cts;
        private int _runCount;
        private int? _maxRuns;
        private bool _monitorProcesses = true;
        private List<string> _programTargets = new List<string>();

        internal List<string> pathsToWatch = new List<string>();

        internal Utils.Watchdog wd = null!;

        public string ServiceName { get; } = "OrcapodMainService";
        
        // Event raised when all monitored processes have exited
        public event EventHandler? AllProcessesExited;

        public MainService()
        {
            LogHandler.Initialize("orcapod.log");
            ReadSettingsFromConfig();
            ConfigBackupHandler.Initialize(null!); // Remove logger dependency
            ConfigBackupHandler.SyncConfigs();
            wd = new Utils.Watchdog(null!); // Remove logger dependency
            wd.FileChanged += (s, e) =>
            {
                ConfigBackupHandler.SyncConfigs();
                LogHandler.LogInfo($"File changed: {e}");
            };
            if (Environment.GetEnvironmentVariable("ORCAPOD_TEST") == "1")
            {
                _interval = TimeSpan.FromSeconds(1);
                _maxRuns = 5;
            }
        }

        // Start the internal work loop
        public void Start()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return;

            LogHandler.LogInfo("MainService starting");
            _cts = new CancellationTokenSource();
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _interval);
        }

        // Stop the internal work loop
        public void Stop()
        {
            if (Interlocked.Exchange(ref _running, 0) == 0)
                return;

            LogHandler.LogInfo("MainService stopping");
            try
            {
                _cts?.Cancel();
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            }
            finally
            {
                _timer?.Dispose();
                _timer = null;
                _cts?.Dispose();
                _cts = null;
                wd.Stop();
            }
        }

        // THIS IS WHERE THE WORK GETS DONE 💪
        private void DoWork(object? state)
        {
            try
            {
                // Put periodic work here.
                // Keep this method cross-platform and lightweight.
                PrintStatusReport();
                var current = Interlocked.Increment(ref _runCount);
                
                // Check if monitored processes are still running
                if (_monitorProcesses && _programTargets.Count > 0)
                {
                    if (!AnyMonitoredProcessRunning())
                    {
                        LogHandler.LogInfo("No monitored processes are running. Shutting down.");
                        AllProcessesExited?.Invoke(this, EventArgs.Empty);
                        try { Stop(); } catch { }
                        return;
                    }
                }
                
                if (_maxRuns.HasValue)
                {
                    if (current >= _maxRuns.Value)
                    {
                        LogHandler.LogInfo($"Max runs reached ({_maxRuns.Value}), stopping MainService.");
                        try { Stop(); } catch { }
                    }
                }
                if (_cts?.IsCancellationRequested == true)
                {
                    LogHandler.LogInfo("Cancellation requested, stopping MainService.");
                    try { Stop(); } catch { }
                }
                if (pathsToWatch.Count > 0)
                {
                    wd.Stop();
                    while (pathsToWatch.Count > 0)
                    {
                        var path = pathsToWatch[0];
                        pathsToWatch.RemoveAt(0);
                        if (System.IO.Directory.Exists(path))
                        {
                            var files = System.IO.Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                wd.AddFile(file);
                            }
                        }
                        else if (System.IO.File.Exists(path))
                        {
                            wd.AddFile(path);
                        }
                        else
                        {
                            LogHandler.LogWarning($"Path '{path}' does not exist as a file or directory, skipping.");
                        }
                    }
                }
                if (!wd.IsRunning)
                {
                    wd.Start();
                    LogHandler.LogInfo("Watchdog started.");
                }
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Exception in MainService.DoWork: {ex}");
            }
        }

        public void Dispose() => Stop();

        private void PrintStatusReport()
        {
            LogHandler.LogInfo($"Status report: ServiceName={ServiceName}, RunCount={_runCount}");
            if (_monitorProcesses && _programTargets.Count > 0)
            {
                LogHandler.LogInfo($"Process monitoring enabled. Checking for: {string.Join(", ", _programTargets)}");
            }
        }

        private void ReadSettingsFromConfig()
        {
            LogHandler.LogInfo("Reading settings from configuration file.");

            // Use SettingsHandler for configuration access
            if (TimeSpan.TryParse(SettingsHandler.Get("General:Interval"), out var interval))
            {
                _interval = interval;
            }
            if (int.TryParse(SettingsHandler.Get("General:MaxRuns"), out var maxRuns))
            {
                _maxRuns = maxRuns;
            }
            
            // Read process monitoring setting
            var monitorProcessesStr = SettingsHandler.Get("General:MonitorProcesses");
            if (!string.IsNullOrEmpty(monitorProcessesStr) && bool.TryParse(monitorProcessesStr, out var monitorProcesses))
            {
                _monitorProcesses = monitorProcesses;
            }
            
            // Read program targets to monitor
            var programTargetsSection = SettingsHandler.GetSection("ProgramTargets");
            if (programTargetsSection != null)
            {
                foreach (var kvp in programTargetsSection.AsEnumerable())
                {
                    if (kvp.Value != null && kvp.Key != "ProgramTargets")
                    {
                        _programTargets.Add(kvp.Value);
                    }
                }
                if (_programTargets.Count > 0)
                {
                    LogHandler.LogInfo($"Monitoring processes: {string.Join(", ", _programTargets)}");
                }
            }

            var mappingsSection = SettingsHandler.GetSection("Mappings");
            if (mappingsSection != null)
            {
                var mappingKeys = new List<string>();
                var mappingValues = new List<string>();
                foreach (var kvp in mappingsSection.AsEnumerable())
                {
                    // Skip the parent section itself
                    if (kvp.Value != null && kvp.Key != "Mappings")
                    {
                        // Remove the "Mappings:" prefix to get the original key
                        var key = kvp.Key.StartsWith("Mappings:") ? kvp.Key.Substring("Mappings:".Length) : kvp.Key;
                        mappingKeys.Add(key);
                        mappingValues.Add(kvp.Value);
                    }
                }
                if (mappingKeys.Count > 0)
                {
                    pathsToWatch.AddRange(mappingKeys);
                    LogHandler.LogInfo($"Added paths from Mappings keys: {string.Join(", ", mappingKeys)}");
                }
                if (mappingValues.Count > 0)
                {
                    pathsToWatch.AddRange(mappingValues);
                    LogHandler.LogInfo($"Added paths from Mappings values: {string.Join(", ", mappingValues)}");
                }
            }
        }
        
        /// <summary>
        /// Checks if any of the monitored processes are currently running.
        /// This is cross-platform compatible.
        /// </summary>
        private bool AnyMonitoredProcessRunning()
        {
            if (_programTargets.Count == 0)
                return true; // No processes to monitor, keep running
            
            try
            {
                var allProcesses = System.Diagnostics.Process.GetProcesses();
                foreach (var target in _programTargets)
                {
                    var targetLower = target.ToLowerInvariant();
                    foreach (var process in allProcesses)
                    {
                        try
                        {
                            var processName = process.ProcessName.ToLowerInvariant();
                            // Check if process name matches (case-insensitive)
                            if (processName == targetLower || processName.Contains(targetLower))
                            {
                                LogHandler.LogInfo($"Found running process: {process.ProcessName} (PID: {process.Id})");
                                return true;
                            }
                        }
                        catch
                        {
                            // Some processes may not be accessible, skip them
                            continue;
                        }
                    }
                }
                LogHandler.LogWarning("None of the monitored processes are currently running.");
                return false;
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Error checking for running processes: {ex.Message}");
                return true; // On error, assume processes are running to avoid premature shutdown
            }
        }
    }
}