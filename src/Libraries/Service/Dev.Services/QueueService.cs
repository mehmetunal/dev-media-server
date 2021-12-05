using Microsoft.Extensions.Hosting;

namespace Dev.Services
{
    public class QueueService : BackgroundService
    {
        private IBackgroundQueueService _queue;

        public QueueService(IBackgroundQueueService queue)
        {
            _queue = queue;
        }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var task = await _queue.PopQueue(stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                using (var source = new CancellationTokenSource())
                {
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    var timeoutToken = source.Token;

                    await task(timeoutToken);
                }
            }
        }
    }
}
