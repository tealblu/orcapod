using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaPod.Service;
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
            IHost host = CreateHostBuilder(args).Build();
            ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
            ILogger<OrcaPod.Service.MainService> mainServiceLogger = host.Services.GetRequiredService<ILogger<OrcaPod.Service.MainService>>();
            bool shouldRunHost = false;

            if (args != null && args.Length > 0)
            {
                var command = args[0].ToLowerInvariant();
                switch (command)
                {
                    case "--console":
                    case "console":
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        shouldRunHost = true;
                        break;
                    case "--install":
                    case "install":
                        HandleInstall(logger);
                        return;
                    case "--uninstall":
                    case "uninstall":
                        HandleUninstall(logger);
                        return;
                    case "--test":
                    case "test":
                        Environment.SetEnvironmentVariable("ORCAPOD_TEST", "1");
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        logger.LogInformation("Test mode enabled.");
                        shouldRunHost = true;
                        break;
                    case "--help":
                    case "help":
                        PrintHelp(logger);
                        return;
                }
            }
            else
            {
                shouldRunHost = ShowMenu(logger, mainServiceLogger);
            }

            if (shouldRunHost)
            {
                await RunHostAsync(host, logger);
            }
        }

        private static async Task RunHostAsync(IHost host, ILogger logger)
        {
            logger.LogInformation("Starting OrcaPod host...");
            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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

        private static void HandleInstall(ILogger logger)
        {
            try
            {
                logger.LogInformation("Autostart install flagged for current user.");
                // TODO: Add actual install logic here
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Install failed: {Message}", ex.Message);
            }
        }

        private static void HandleUninstall(ILogger logger)
        {
            try
            {
                logger.LogInformation("Autostart removal flagged for current user.");
                // TODO: Add actual uninstall logic here
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Uninstall failed: {Message}", ex.Message);
            }
        }

        private class MenuOption
        {
            public required string Key { get; set; }
            public required string Description { get; set; }
            public required Action Action { get; set; }
            public bool ExitAfter { get; set; } = false;
        }

    private static bool ShowMenu(ILogger logger, ILogger<OrcaPod.Service.MainService> mainServiceLogger)
        {
            var shouldRunHost = false;
            var menuOptions = new[]
            {
                new MenuOption {
                    Key = "1",
                    Description = "Run in interactive console mode",
                    Action = () => {
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        logger.LogInformation("Console mode enabled.");
                        shouldRunHost = true;
                    },
                    ExitAfter = true
                },
                new MenuOption {
                    Key = "2",
                    Description = "Install per-user autostart",
                    Action = () => HandleInstall(logger)
                },
                new MenuOption {
                    Key = "3",
                    Description = "Remove per-user autostart",
                    Action = () => HandleUninstall(logger)
                },
                new MenuOption {
                    Key = "4",
                    Description = "Run in test mode",
                    Action = () => {
                        Environment.SetEnvironmentVariable("ORCAPOD_TEST", "1");
                        Environment.SetEnvironmentVariable("ORCAPOD_CONSOLE", "1");
                        logger.LogInformation("Test mode enabled.");
                        shouldRunHost = true;
                    },
                    ExitAfter = true
                },
                new MenuOption {
                    Key = "5",
                    Description = "Create backup of config(s)",
                    Action = () => {
                        var backupHandler = new ConfigBackupHandler(mainServiceLogger);
                        backupHandler.BackupConfigs();
                    }
                },
                new MenuOption {
                    Key = "6",
                    Description = "Show help",
                    Action = () => PrintHelp(logger)
                },
                new MenuOption {
                    Key = "0",
                    Description = "Exit",
                    Action = () => {
                        logger.LogInformation("Exiting.");
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

        private static void PrintHelp(ILogger logger)
        {
            logger.LogInformation("OrcaPod CLI Usage:");
            logger.LogInformation("  --console      Run in interactive console mode");
            logger.LogInformation("  --install      Install per-user autostart");
            logger.LogInformation("  --uninstall    Remove per-user autostart");
            logger.LogInformation("  --test         Run in test mode (sets ORCAPOD_TEST=1)");
            logger.LogInformation("  --help         Show this help message");
        }
    }
}