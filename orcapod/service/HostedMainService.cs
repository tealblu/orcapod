using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace OrcaPod.Service
{
    // Wrap the cross-platform MainService in a BackgroundService so it participates in the Generic Host lifecycle
    public class HostedMainService : BackgroundService
    {
        private readonly MainService _service;

        public HostedMainService(MainService service)
        {
            _service = service;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
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
            _service.Stop();
            return Task.CompletedTask;
        }
    }
}
