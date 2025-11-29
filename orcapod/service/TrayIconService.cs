using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orcapod.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS
using System.Windows.Forms;
#elif LINUX
using H.NotifyIcon.Core;
using System.IO;
#endif

namespace OrcaPod.Service
{
    // Cross-platform tray icon service for Windows and Linux (X11/Wayland)
    public class TrayIconService : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime _lifetime;
        // Remove injected logger, use LogHandler
#if WINDOWS
        private NotifyIcon? _notifyIcon;
#elif LINUX
        private TrayIcon? _trayIcon;
#endif

        public TrayIconService(IHostApplicationLifetime lifetime, ILogger<TrayIconService> logger)
        {
            _lifetime = lifetime;
            LogHandler.Initialize("orcapod.log");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                LogHandler.LogInfo("Starting TrayIconService");
#if WINDOWS
                var menu = new ContextMenuStrip();
                var exitItem = new ToolStripMenuItem("Exit");
                exitItem.Click += (s, e) => _lifetime.StopApplication();
                menu.Items.Add(exitItem);
                _notifyIcon = new NotifyIcon()
                {
                    Icon = System.Drawing.SystemIcons.Application,
                    Visible = true,
                    Text = "OrcaPod (per-user)",
                    ContextMenuStrip = menu
                };
                _notifyIcon.DoubleClick += (s, e) =>
                {
                    _notifyIcon?.ShowBalloonTip(3000, "OrcaPod", "Running in background", ToolTipIcon.Info);
                };
#elif LINUX
                _trayIcon = new TrayIcon
                {
                    ToolTip = "OrcaPod (per-user)",
                    Icon = IntPtr.Zero  // TODO: Load icon properly
                };
                
                // Handle clicks
                _trayIcon.MessageWindow.MouseEventReceived += (s, e) =>
                {
                    if (e.MouseEvent == MouseEvent.IconLeftMouseUp)
                    {
                        LogHandler.LogInfo("OrcaPod is running in background");
                    }
                };
                
                _trayIcon.Create();
                LogHandler.LogInfo("Linux tray icon created successfully");
#endif
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogHandler.LogError($"Failed to start tray icon: {ex}");
                return Task.CompletedTask;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            LogHandler.LogInfo("Stopping TrayIconService");
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
#if WINDOWS
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
#elif LINUX
                if (_trayIcon != null)
                {
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
#endif
            }
            catch { }
        }

#if LINUX
        private string GetIconPath()
        {
            // Try to find a suitable icon
            var iconPaths = new[]
            {
                "/usr/share/pixmaps/orca.png",
                "/usr/share/icons/hicolor/48x48/apps/orca.png",
                "/usr/share/icons/gnome/48x48/apps/application-x-executable.png",
                "/usr/share/icons/hicolor/48x48/apps/application-x-executable.png"
            };

            foreach (var path in iconPaths)
            {
                if (File.Exists(path))
                {
                    LogHandler.LogInfo($"Using icon: {path}");
                    return path;
                }
            }

            // Fallback to a generic icon name that should be available on most systems
            LogHandler.LogInfo("Using fallback icon: application-x-executable");
            return "application-x-executable";
        }
#endif
    }
}
