namespace Dev.Services
{
    public interface IBackgroundQueueService
    {
        void QueueTask(Func<CancellationToken, Task> task);

        Task<Func<CancellationToken, Task>> PopQueue(CancellationToken cancellationToken);
    }
}
