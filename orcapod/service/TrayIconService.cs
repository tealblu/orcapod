#if WINDOWS
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace OrcaPod.Service
{
    // A minimal per-user system tray hosted service for Windows.
    // It creates a NotifyIcon and exposes a small context menu to stop the host.
    public class TrayIconService : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<TrayIconService> _logger;
        private NotifyIcon? _notifyIcon;

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

                // Create a simple context menu
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

                // Optionally handle double-click to show a status, etc.
                _notifyIcon.DoubleClick += (s, e) =>
                {
                    // Example: show balloon tip
                    _notifyIcon?.ShowBalloonTip(3000, "OrcaPod", "Running in background", ToolTipIcon.Info);
                };

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
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch { }
        }
    }
}
#endif
