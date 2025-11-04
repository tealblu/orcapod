using System;
using System.Linq;
using System.Threading;
using Timer = System.Threading.Timer;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OrcaPod.Utils;

namespace OrcaPod.Service
{
    // Cross-platform core service (no Windows-only APIs)
    public class MainService : IDisposable
    {
        private readonly ILogger<MainService> _logger;
        private Timer? _timer;
        private TimeSpan _interval = TimeSpan.FromSeconds(5);
        private int _running;
        private CancellationTokenSource? _cts;
        private int _runCount;
        private int? _maxRuns;

        internal List<string> pathsToWatch = new List<string>();

        internal Utils.Watchdog wd = null!;

        public string ServiceName { get; } = "OrcapodMainService";

        public MainService(ILogger<MainService> logger)
        {
            _logger = logger;
            wd = new Utils.Watchdog(_logger);

            wd.FileChanged += (s, e) =>
            {
                ConfigBackupHandler.BackupConfigs();
                _logger.LogInformation($"File changed: {e}");
            };

            // Allow a quick test mode via environment variable ORCAPOD_TEST=1
            if (Environment.GetEnvironmentVariable("ORCAPOD_TEST") == "1")
            {
                _interval = TimeSpan.FromSeconds(1);
                _maxRuns = 5; // stop after a few iterations in test mode
            }

            ConfigBackupHandler.Initialize(_logger);
            
            ReadSettingsFromConfig();
        }

        // Start the internal work loop
        public void Start()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
                return;

            _logger.LogInformation("MainService starting");
            _cts = new CancellationTokenSource();
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _interval);
        }

        // Stop the internal work loop
        public void Stop()
        {
            if (Interlocked.Exchange(ref _running, 0) == 0)
                return;

            _logger.LogInformation("MainService stopping");
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

                // Check for max runs in test mode
                if (_maxRuns.HasValue)
                {
                    if (current >= _maxRuns.Value)
                    {
                        // Stop after configured runs in test mode
                        _logger.LogInformation($"Max runs reached ({_maxRuns.Value}), stopping MainService.");
                        try { Stop(); } catch { }
                    }
                }

                // Check for cancellation
                if (_cts?.IsCancellationRequested == true)
                {
                    _logger.LogInformation("Cancellation requested, stopping MainService.");
                    try { Stop(); } catch { }
                }

                // Add any paths that need to be watched
                if (pathsToWatch.Count > 0)
                {
                    wd.Stop();

                    while (pathsToWatch.Count > 0)
                    {
                        var path = pathsToWatch[0];
                        pathsToWatch.RemoveAt(0);
                        wd.AddFile(path);
                    }
                }

                // Check for and load configs updated since last check
                ConfigBackupHandler.LoadUpdatedConfigs();

                // Start the watchdog if not running
                if (!wd.IsRunning)
                {
                    wd.Start();
                    _logger.LogInformation("Watchdog started.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in MainService.DoWork");
            }
        }

        public void Dispose() => Stop();

        private void PrintStatusReport()
        {
            _logger.LogInformation($"Status report: ServiceName={ServiceName}, RunCount={_runCount}");
        }

        private void ReadSettingsFromConfig()
        {
            _logger.LogInformation("Reading settings from configuration file.");

            // Use SettingsHandler for configuration access
            if (TimeSpan.TryParse(SettingsHandler.Get("General:Interval"), out var interval))
            {
                _interval = interval;
            }
            if (int.TryParse(SettingsHandler.Get("General:MaxRuns"), out var maxRuns))
            {
                _maxRuns = maxRuns;
            }

            var mappingsSection = SettingsHandler.GetSection("Mappings");
            if (mappingsSection != null)
            {
                var mappingKeys = new List<string>();
                foreach (var kvp in mappingsSection.AsEnumerable())
                {
                    // Skip the parent section itself
                    if (kvp.Value != null && kvp.Key != "Mappings")
                    {
                        // Remove the "Mappings:" prefix to get the original key
                        var key = kvp.Key.StartsWith("Mappings:") ? kvp.Key.Substring("Mappings:".Length) : kvp.Key;
                        mappingKeys.Add(key);
                    }
                }
                if (mappingKeys.Count > 0)
                {
                    pathsToWatch.AddRange(mappingKeys);
                    _logger.LogInformation($"Added paths from Mappings keys: {string.Join(", ", mappingKeys)}");
                }
            }
        }
    }
}