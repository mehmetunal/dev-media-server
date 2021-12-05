using System.Collections.Concurrent;

namespace Dev.Services
{
    public class BackgroundQueueService : IBackgroundQueueService
    {
        private ConcurrentQueue<Func<CancellationToken, Task>> Tasks;

        private SemaphoreSlim Signal;

        public BackgroundQueueService()
        {
            Tasks = new ConcurrentQueue<Func<CancellationToken, Task>>();
            Signal = new SemaphoreSlim(0);
        }

        public void QueueTask(Func<CancellationToken, Task> task)
        {
            Tasks.Enqueue(task);
            Signal.Release();
        }

        public async Task<Func<CancellationToken, Task>> PopQueue(CancellationToken cancellationToken)
        {
            await Signal.WaitAsync(cancellationToken);
            Tasks.TryDequeue(out var task);

            return task;
        }
    }
}
