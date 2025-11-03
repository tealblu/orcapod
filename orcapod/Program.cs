using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaPod.Service;
using OrcaPod;
using OrcaPod.Utils;
using System.Threading;
using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;

namespace OrcaPod
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var service = new MainService();

            // Support a simple command-line helper for local testing: --console forces running interactively
            if (args != null && args.Length > 0)
            {
                if (args.Contains("--console", StringComparer.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                }
            }

            // No platform-specific daemon/service handling — always run the Generic Host.
            // Tray icon support will be registered for interactive sessions on Windows and Linux.

            // Otherwise, build a Generic Host. This covers interactive console runs, and the
            // per-user system tray scenario on Windows (registered as a hosted service below).
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, services) =>
                {
                    // Core cross-platform service and utilities
                    services.AddSingleton(service);
                    services.AddSingleton<Watchdog>();

                    // Host the MainService via a BackgroundService so it integrates with the Generic Host lifecycle
                    services.AddHostedService<HostedMainService>();

                    // On Windows and Linux interactive sessions, add the system tray hosted service
                    if ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                        && Environment.UserInteractive)
                    {
                        services.AddHostedService<TrayIconService>();
                    }
                })
                .ConfigureLogging((ctx, lb) => lb.AddConsole());

            var host = hostBuilder.Build();

            // Run the host which starts hosted services and blocks until shutdown.
            await host.RunAsync();

            // Host ran and exited. All shutdown work is handled by hosted services.
        }
    }
}