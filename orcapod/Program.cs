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

            // Support simple CLI helpers:
            // --console : force interactive console behavior
            // --install : install per-user autostart
            // --uninstall : remove per-user autostart
            // --test : run a short test mode (sets ORCAPOD_TEST=1)
            if (args != null && args.Length > 0)
            {
                if (args.Contains("--console", StringComparer.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                }

                if (args.Contains("--install", StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        Console.WriteLine("Autostart install flagged for current user.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Install failed: " + ex.Message);
                        return;
                    }
                    return;
                }

                if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        Console.WriteLine("Autostart removal flagged for current user.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Uninstall failed: " + ex.Message);
                        return;
                    }
                    return;
                }

                if (args.Contains("--test", StringComparer.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable("ORCAPOD_TEST", "1");
                    // Also run in console mode so output is visible
                    Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");

                    Console.WriteLine("Test mode enabled.");
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
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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