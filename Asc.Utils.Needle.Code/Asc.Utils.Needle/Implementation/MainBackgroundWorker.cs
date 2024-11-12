using System.Collections.Concurrent;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal sealed class MainBackgroundWorker : IBackgroundSemaphoreWorker
{
    #region Singleton stuff

    private MainBackgroundWorker() { }

    private static readonly Lazy<IBackgroundSemaphoreWorker> lazyInstance = new(
        () => new MainBackgroundWorker(),
        LazyThreadSafetyMode.PublicationOnly
    );

    public static IBackgroundSemaphoreWorker Instance => lazyInstance.Value;

    #endregion

    private static readonly ReaderWriterLockSlim locker = new();
    private readonly SemaphoreSlim semaphore = new(Environment.ProcessorCount);
    private readonly ConcurrentBag<Task> tasks = [];
    private Task? runTask = null;

    public event EventHandler? JobCompleted;
    public event EventHandler<Exception>? JobFaulted;

    public int CurrentJobsStackSize => tasks.Count;

    public bool IsRunning => runTask != null && runTask.Status == TaskStatus.Running;

    public async Task AddJobAsync(Action job)
    {
        await AddJobAsyncInternal(job);

        if (IsRunning)
            return;

        BeginRun();
    }

    public async Task AddJobAsync(Func<Task> job)
    {
        await AddJobAsyncInternal(job);

        if (IsRunning)
            return;

        BeginRun();
    }

    public async Task AddJobAsyncInternal(object job)
    {
        ArgumentNullException.ThrowIfNull(nameof(job));

        await semaphore.WaitAsync();

        tasks.Add(Task.Run(async () =>
        {
            bool wasSuccess = true;

            try
            {
                if (job is Func<Task> asyncJob)
                    await asyncJob();
                else
                {
                    Action syncJob = (Action)job;
                    syncJob();
                }
            }
            catch (Exception ex)
            {
                wasSuccess = false;
                JobFaulted?.Invoke(this, ex);
            }
            finally
            {
                if (wasSuccess)
                    JobCompleted?.Invoke(this, EventArgs.Empty);

                semaphore.Release();
            }
        }));
    }

    public override string ToString()
    {
        return $"IsRunning = {IsRunning}, CurrentJobsStackSize = {CurrentJobsStackSize}";
    }

    private void BeginRun()
    {
        if (tasks.IsEmpty)
            return;

        runTask = Task.Run(async () =>
        {
            await Task.WhenAll(tasks);
        });
    }

    private string GetDebuggerDisplay()
    {
        return ToString();
    }
}
