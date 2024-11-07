using System.Collections.Concurrent;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class MasterNeddleWorker : IMasterNeddleWorker
{
    #region Singleton stuff

    private MasterNeddleWorker() { }

    private static readonly Lazy<IMasterNeddleWorker> lazyInstance = new(
        () => new MasterNeddleWorker(),
        LazyThreadSafetyMode.PublicationOnly
    );

    public static IMasterNeddleWorker Instance => lazyInstance.Value;

    #endregion

    private readonly SemaphoreSlim semaphore = new(3);
    private readonly ConcurrentBag<Task> tasks = [];
    private Task? runTask = null;

    public event EventHandler? JobCompleted;
    public event EventHandler<Exception>? JobFaulted;

    public int CurrentJobsStackSize => tasks.Count;
    public bool IsRunning => runTask != null && runTask.Status == TaskStatus.Running;

    public async Task AddJobAsync(Action job)
    {
        ArgumentNullException.ThrowIfNull(nameof(job));

        await semaphore.WaitAsync();

        tasks.Add(Task.Run(() =>
        {
            bool wasSuccess = true;

            try
            {
                job();
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

        if (IsRunning)
            return;

        BeginRun();
    }

    public async Task AddJobAsync(Func<Task> job)
    {
        ArgumentNullException.ThrowIfNull(nameof(job));

        await semaphore.WaitAsync();

        tasks.Add(Task.Run(async () =>
        {
            bool wasSuccess = true;

            try
            {
                await job();
            }
            catch (Exception ex)
            {
                wasSuccess |= false;
                JobFaulted?.Invoke(this, ex);
            }
            finally
            {
                if (wasSuccess)
                    JobCompleted?.Invoke(this, EventArgs.Empty);

                semaphore.Release();
            }
        }));

        if (IsRunning)
            return;

        BeginRun();
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
