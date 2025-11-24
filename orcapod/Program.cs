using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaPod.Service;
using OrcaPod.Utils;
using System.Threading;
using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;
using Velopack;
using Orcapod.Utils;

namespace OrcaPod
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            VelopackApp.Build().Run();

            IHost host = CreateHostBuilder(args).Build();
            LogHandler.Initialize("orcapod.log");
            InstallHandler.Initialize();
            bool shouldRunHost = false;

            if (args != null && args.Length > 0)
            {
                var command = args[0].ToLowerInvariant();
                switch (command)
                {
                    case "--background":
                    case "--service":
                    case "background":
                    case "service":
                        shouldRunHost = true;
                        break;
                    case "--console":
                    case "console":
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        shouldRunHost = true;
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
                        LogHandler.LogInfo("Test mode enabled.");
                        shouldRunHost = true;
                        break;
                    case "--help":
                    case "help":
                        PrintHelp();
                        return;
                }
            }
            else
            {
                shouldRunHost = ShowMenu();
            }

            if (shouldRunHost)
            {
                await RunHostAsync(host);
            }
        }

        private static async Task RunHostAsync(IHost host)
        {
            LogHandler.LogInfo("Starting OrcaPod host...");
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<MainService>();
                    services.AddSingleton<Watchdog>();
                    services.AddHostedService<HostedMainService>();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        services.AddHostedService<TrayIconService>();
                    }
                });

        private static void HandleInstall()
        {
            try
            {
                LogHandler.LogInfo("Autostart install flagged for current user.");
                InstallHandler.HandleInstall();
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Install failed: {ex.Message}");
            }
        }

        private static void HandleUninstall()
        {
            try
            {
                LogHandler.LogInfo("Autostart removal flagged for current user.");
                InstallHandler.HandleUninstall();
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Uninstall failed: {ex.Message}");
            }
        }

        private class MenuOption
        {
            public required string Key { get; set; }
            public required string Description { get; set; }
            public required Action Action { get; set; }
            public bool ExitAfter { get; set; } = false;
        }

        private static bool ShowMenu()
        {
            var shouldRunHost = false;
            var menuOptions = new[]
            {
                new MenuOption {
                    Key = "1",
                    Description = "Run in interactive console mode",
                    Action = () => {
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        LogHandler.LogInfo("Console mode enabled.");
                        shouldRunHost = true;
                    },
                    ExitAfter = true
                },
                new MenuOption {
                    Key = "2",
                    Description = "Install autostart for configured programs",
                    Action = () => HandleInstall()
                },
                new MenuOption {
                    Key = "3",
                    Description = "Remove autostart for configured programs",
                    Action = () => HandleUninstall()
                },
                new MenuOption {
                    Key = "4",
                    Description = "Run in test mode",
                    Action = () => {
                        Environment.SetEnvironmentVariable("ORCAPOD_TEST", "1");
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        LogHandler.LogInfo("Test mode enabled.");
                        shouldRunHost = true;
                    },
                    ExitAfter = true
                },
                new MenuOption {
                    Key = "5",
                    Description = "Create backup of config(s)",
                    Action = () => {
                        ConfigBackupHandler.BackupConfigs();
                    }
                },
                new MenuOption {
                    Key = "6",
                    Description = "Show help",
                    Action = () => PrintHelp()
                },
                new MenuOption {
                    Key = "0",
                    Description = "Exit",
                    Action = () => {
                        LogHandler.LogInfo("Exiting.");
                        Environment.Exit(0);
                    },
                    ExitAfter = true
                }
            };

            while (true)
            {
                Console.WriteLine("\nOrcaPod Menu:");
                foreach (var option in menuOptions)
                {
                    Console.WriteLine($"      {option.Key}. {option.Description}");
                }
                Console.Write("Select an option: ");

                var input = Console.ReadLine();
                var selected = Array.Find(menuOptions, o => o.Key == input);
                if (selected != null)
                {
                    selected.Action();
                    if (selected.ExitAfter)
                        break;
                }
                else
                {
                    Console.WriteLine("Invalid option. Please try again.");
                }
            }
            return shouldRunHost;
        }

        private static void PrintHelp()
        {
            LogHandler.LogInfo("OrcaPod CLI Usage:");
            LogHandler.LogInfo("  --console      Run in interactive console mode");
            LogHandler.LogInfo("  --install      Install per-user autostart");
            LogHandler.LogInfo("  --uninstall    Remove per-user autostart");
            LogHandler.LogInfo("  --test         Run in test mode (sets ORCAPOD_TEST=1)");
            LogHandler.LogInfo("  --help         Show this help message");
        }
    }
}