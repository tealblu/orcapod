using System;
using System.Linq;
using System.Threading;
using Timer = System.Threading.Timer;

namespace OrcaPod.Service
{
    // Cross-platform core service (no Windows-only APIs)
    public class MainService : IDisposable
    {
        private Timer? _timer;
        private TimeSpan _interval = TimeSpan.FromSeconds(10);
        private int _running;
        private CancellationTokenSource? _cts;
        private int _runCount;
        private int? _maxRuns;

        public string ServiceName { get; } = "OrcapodMainService";

        public MainService()
        {
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

            _cts = new CancellationTokenSource();
            _timer = new Timer(DoWork, null, TimeSpan.Zero, _interval);
        }

        // Stop the internal work loop
        public void Stop()
        {
            if (Interlocked.Exchange(ref _running, 0) == 0)
                return;

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
                        try { Stop(); } catch { }
                    }
                }

                // Implement the rest of the periodic work here.
            }
            catch (Exception)
            {
                // Handle/log exceptions appropriately (left generic here).
            }
        }

        public void Dispose() => Stop();
    }
}