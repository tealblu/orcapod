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
            if (args != null && args.Length > 0)
            {
                var command = args[0].ToLowerInvariant();
                switch (command)
                {
                    case "--console":
                    case "console":
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        break;
                    case "--install":
                    case "install":
                        HandleInstall();
                        return;
                    case "--uninstall":
                    case "uninstall":
                        HandleUninstall();
                        return;
                    case "--test":
                    case "test":
                        Environment.SetEnvironmentVariable("ORCAPOD_TEST", "1");
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        Console.WriteLine("Test mode enabled.");
                        break;
                    case "--help":
                    case "help":
                        PrintHelp();
                        return;
                }
            }
            else
            {
                ShowMenu();
            }

            // Build and run the Generic Host as before
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<MainService>();
                    services.AddSingleton<Watchdog>();
                    services.AddHostedService<HostedMainService>();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        services.AddHostedService<TrayIconService>();
                    }
                })
                .ConfigureLogging((ctx, lb) => lb.AddConsole());

            var host = hostBuilder.Build();
            await host.RunAsync();
        }

        private static void HandleInstall()
        {
            try
            {
                Console.WriteLine("Autostart install flagged for current user.");
                // TODO: Add actual install logic here
            }
            catch (Exception ex)
            {
                Console.WriteLine("Install failed: " + ex.Message);
            }
        }

        private static void HandleUninstall()
        {
            try
            {
                Console.WriteLine("Autostart removal flagged for current user.");
                // TODO: Add actual uninstall logic here
            }
            catch (Exception ex)
            {
                Console.WriteLine("Uninstall failed: " + ex.Message);
            }
        }

        private static void ShowMenu()
        {
            while (true)
            {
                Console.WriteLine("\nOrcaPod Menu:");
                Console.WriteLine("1. Run in interactive console mode");
                Console.WriteLine("2. Install per-user autostart");
                Console.WriteLine("3. Remove per-user autostart");
                Console.WriteLine("4. Run in test mode");
                Console.WriteLine("5. Show help");
                Console.WriteLine("0. Exit");
                Console.Write("Select an option: ");

                var input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        Console.WriteLine("Console mode enabled.");
                        return;
                    case "2":
                        HandleInstall();
                        break;
                    case "3":
                        HandleUninstall();
                        break;
                    case "4":
                        Environment.SetEnvironmentVariable("ORCAPOD_TEST", "1");
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        Console.WriteLine("Test mode enabled.");
                        return;
                    case "5":
                        PrintHelp();
                        break;
                    case "0":
                        Console.WriteLine("Exiting.");
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("OrcaPod CLI Usage:");
            Console.WriteLine("  --console      Run in interactive console mode");
            Console.WriteLine("  --install      Install per-user autostart");
            Console.WriteLine("  --uninstall    Remove per-user autostart");
            Console.WriteLine("  --test         Run in test mode (sets ORCAPOD_TEST=1)");
            Console.WriteLine("  --help         Show this help message");
        }
    }
}