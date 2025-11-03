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
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);
        private int _running;
        private CancellationTokenSource? _cts;

        public string ServiceName { get; } = "OrcapodMainService";

        public MainService() { }

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
            }
            catch (Exception)
            {
                // Handle/log exceptions appropriately (left generic here).
            }
        }

        public void Dispose() => Stop();
    }

    // Platform hosts (Windows service wrapper, systemd wrapper, etc.)
    // should implement this interface in a separate file/assembly.
    public interface IPlatformServiceHost
    {
        // Called to run the provided MainService as a hosted service.
        // The platform-specific host is responsible for calling Start/Stop at the right times.
        void RunHosted(MainService service);
    }
}