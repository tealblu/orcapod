// ...existing code...
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
#if WINDOWS
using System.Windows.Forms;
#elif LINUX
using H.NotifyIcon.Core;
#endif

namespace OrcaPod.Service
{
    // Cross-platform tray icon service for Windows and Linux (X11/Wayland)
    public class TrayIconService : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<TrayIconService> _logger;
#if WINDOWS
        private NotifyIcon? _notifyIcon;
#elif LINUX
        private TaskbarIcon? _notifyIcon;
#endif

        public TrayIconService(IHostApplicationLifetime lifetime, ILogger<TrayIconService> logger)
        {
            _lifetime = lifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting TrayIconService");
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
                _notifyIcon = new TaskbarIcon();
                _notifyIcon.ToolTipText = "OrcaPod (per-user)";
                // TODO: Set icon and menu for Linux tray
#endif
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start tray icon");
                return Task.CompletedTask;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping TrayIconService");
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
                if (_notifyIcon != null)
                {
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
#endif
            }
            catch { }
        }
    }
}
