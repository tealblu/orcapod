using System;
using System.Threading;
using OrcaPod.Service;

namespace OrcaPod.Service
{
    // Minimal cross-platform daemon host. When discovered by Program.Main,
    // this will run the cross-platform MainService, hook process exit signals,
    // and block until shutdown is requested.
    public class OrcaPodDaemon : IPlatformServiceHost
    {
        public void RunHosted(MainService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var stopEvent = new ManualResetEventSlim(false);

            void Shutdown()
            {
                try { service.Stop(); }
                catch { }
                finally { stopEvent.Set(); }
            }

            // Handle Ctrl+C in interactive envs where a terminal is attached.
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true; // allow graceful shutdown
                Shutdown();
            };

            // Ensure we stop the service when the process exits.
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();

            service.Start();
            Console.WriteLine($"{service.ServiceName} running as daemon. Waiting for shutdown...");

            // Block until shutdown is requested.
            stopEvent.Wait();
        }
    }
}
