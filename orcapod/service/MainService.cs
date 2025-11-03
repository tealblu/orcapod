using System;
using System.Linq;
using System.Threading;
using Timer = System.Threading.Timer;

using Microsoft.Extensions.Logging;

namespace OrcaPod.Service
{
    // Cross-platform core service (no Windows-only APIs)
    public class MainService : IDisposable
    {
        private readonly ILogger<MainService> _logger;
        private Timer? _timer;
        private TimeSpan _interval = TimeSpan.FromSeconds(10);
        private int _running;
        private CancellationTokenSource? _cts;
        private int _runCount;
        private int? _maxRuns;

        public string ServiceName { get; } = "OrcapodMainService";

        public MainService(ILogger<MainService> logger)
        {
            _logger = logger;
            // Allow a quick test mode via environment variable ORCAPOD_TEST=1
            if (Environment.GetEnvironmentVariable("ORCAPOD_TEST") == "1")
            {
                _interval = TimeSpan.FromSeconds(1);
                _maxRuns = 5; // stop after a few iterations in test mode
            }
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
            }
        }

        // THIS IS WHERE THE WORK GETS DONE
        private void DoWork(object? state)
        {
            try
            {
                // Put periodic work here.
                // Keep this method cross-platform and lightweight.

                // Check for max runs in test mode
                if (_maxRuns.HasValue)
                {
                    var current = Interlocked.Increment(ref _runCount);
                    if (current >= _maxRuns.Value)
                    {
                        // Stop after configured runs in test mode
                        _logger.LogInformation($"Max runs reached ({_maxRuns.Value}), stopping MainService.");
                        try { Stop(); } catch { }
                    }
                }

                // Implement the rest of the periodic work here.
                PrintStatusReport();
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
    }
}