using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

using Orcapod.Utils;
namespace OrcaPod.Service
{
    // Wrap the cross-platform MainService in a BackgroundService so it participates in the Generic Host lifecycle
    public class HostedMainService : BackgroundService
    {
        private readonly MainService _service;
        private readonly IHostApplicationLifetime _appLifetime;

        public HostedMainService(MainService service, IHostApplicationLifetime appLifetime)
        {
            _service = service;
            _appLifetime = appLifetime;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            // Subscribe to the event that fires when all monitored processes have exited
            _service.AllProcessesExited += OnAllProcessesExited;
            _service.Start();
            return Task.CompletedTask;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Keep running until the host signals cancellation. When cancellation occurs we'll stop the underlying service.
            var tcs = new TaskCompletionSource<object?>();
            stoppingToken.Register(() => tcs.TrySetResult(null));
            return tcs.Task;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _service.AllProcessesExited -= OnAllProcessesExited;
            _service.Stop();
            return Task.CompletedTask;
        }
        
        private void OnAllProcessesExited(object? sender, System.EventArgs e)
        {
            LogHandler.LogInfo("All monitored processes have exited. Requesting application shutdown.");
            // Request graceful shutdown of the application
            _appLifetime.StopApplication();
        }
    }
}
