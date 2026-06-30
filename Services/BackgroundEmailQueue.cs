using System.Threading.Channels;

namespace Safi_Ticket.Services
{
    public class BackgroundEmailQueue : BackgroundService
    {
        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue =
            Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundEmailQueue> _logger;

        public BackgroundEmailQueue(
            IServiceProvider serviceProvider,
            ILogger<BackgroundEmailQueue> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            if (!_queue.Writer.TryWrite(workItem))
            {
                _logger.LogWarning("Failed to queue background email work item.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var workItem in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Background email work item failed.");
                }
            }
        }
    }
}
