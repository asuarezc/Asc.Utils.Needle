using System.Collections.Concurrent;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class SemaphoreWorkerSlim : INeedleWorkerSlim
{
    private bool disposedValue = false;
    private bool canceledEventAlreadyRaised = false;
    private bool isRunning = false;
    private CancellationTokenSource cancellationTokenSource = new();

    private static readonly object lockObject = new();
    private readonly ReaderWriterLockSlim locker;
    private readonly SemaphoreSlim semaphore;
    private readonly ConcurrentBag<Action> actionJobs = [];
    private readonly ConcurrentBag<Func<Task>> taskJobs = [];
    private readonly ConcurrentBag<Exception> exceptions = [];
    private readonly List<Task> tasks = [];

    public SemaphoreWorkerSlim()
        : this(Environment.ProcessorCount, true) { }

    public SemaphoreWorkerSlim(int numberOfThreads)
        : this(numberOfThreads, true) { }

    public SemaphoreWorkerSlim(bool cancelPendingJobsIfAnyOtherFails)
        : this(Environment.ProcessorCount, cancelPendingJobsIfAnyOtherFails) { }

    public SemaphoreWorkerSlim(int numberOfThreads, bool cancelPendingJobsIfAnyOtherFails)
    {
        if (numberOfThreads <= 0)
            throw new ArgumentException($"Param {numberOfThreads} must be greater than zero", nameof(numberOfThreads));

        locker = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        semaphore = new SemaphoreSlim(numberOfThreads);
        CancelPendingJobsIfAnyOtherFails = cancelPendingJobsIfAnyOtherFails;
    }

    #region INeedleWorkerSlim implementation

    public event EventHandler? Canceled;

    public CancellationToken CancellationToken
    {
        get
        {
            ThrowIfDisposed();
            return cancellationTokenSource.Token;
        }
    }

    public bool CancelPendingJobsIfAnyOtherFails { get; private set; }

    public virtual void AddJob(Action job)
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(job);

        actionJobs.Add(job);
    }

    public virtual void AddJob(Func<Task> job)
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(job);

        taskJobs.Add(job);
    }

    public void Cancel()
    {
        ThrowIfDisposed();
        ThrowIfNotRunning();
        ThrowIfCancellationAlreadyRequested();

        cancellationTokenSource.Cancel();
        RaiseCanceled();
    }

    public virtual async Task RunAsync()
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ThrowIfThereIsNoJobsToRun();
        SetIsRunning(true);

        try
        {
            await RunInternalAsync();
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            SetIsRunning(false);
            ClearWorkCollections();
            ResetCancellationToken();
            canceledEventAlreadyRaised = false;
        }
    }

    #endregion

    #region Protected methods

    protected virtual void AddJobActionToSemaphore(Action job)
    {
        AddTaskToSemaphore(Task.Run(() => RunJob(job)));
    }

    protected virtual void AddJobTaskToSemaphore(Func<Task> job)
    {
        AddTaskToSemaphore(Task.Run(async () => await RunJobAsync(job)));
    }

    protected virtual void ManageException(Exception ex)
    {
        if (!CancellationToken.IsCancellationRequested && CancelPendingJobsIfAnyOtherFails)
            Cancel();

        exceptions.Add(ex);
    }

    protected virtual void ThrowIfRunning()
    {
        if (IsRunning())
            throw new InvalidOperationException("Cannot do this operation while running");
    }

    protected virtual void ThrowIfNotRunning()
    {
        if (!IsRunning())
            throw new InvalidOperationException("Cannot do this operation while not running");
    }

    protected void RaiseCanceled()
    {
        lock (lockObject)
        {
            if (canceledEventAlreadyRaised)
                return;

            Canceled?.Invoke(this, EventArgs.Empty);
            canceledEventAlreadyRaised = true;
        }
    }

    protected void ReleaseSemaphore()
    {
        semaphore.Release();
    }

    protected void AddTaskToSemaphore(Task task)
    {
        tasks.Add(task);
    }

    protected async Task RunInternalAsync()
    {
        foreach (Action job in actionJobs)
        {
            await semaphore.WaitAsync();
            AddJobActionToSemaphore(job);
        }

        foreach (Func<Task> job in taskJobs)
        {
            await semaphore.WaitAsync();
            AddJobTaskToSemaphore(job);
        }

        await Task.WhenAll(tasks);

        if (!exceptions.IsEmpty)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", exceptions);
    }

    protected void ClearWorkCollections()
    {
        actionJobs.Clear();
        taskJobs.Clear();
        exceptions.Clear();
        tasks.Clear();
    }

    protected void ResetCancellationToken()
    {
        cancellationTokenSource = new();
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
    }

    protected void ThrowIfCancellationAlreadyRequested()
    {
        if (CancellationToken.IsCancellationRequested)
            throw new InvalidOperationException("Cannot do this operation since cancellation has been requested");
    }

    protected void ThrowIfThereIsNoJobsToRun()
    {
        if (actionJobs.IsEmpty && taskJobs.IsEmpty)
            throw new InvalidOperationException("Nothing to run. Add jobs before running a worker");
    }

    #endregion

    private bool IsRunning()
    {
        bool workerIsRunning;

        locker.EnterReadLock();
        try { workerIsRunning = isRunning; }
        finally { locker.ExitReadLock(); }

        return workerIsRunning;
    }

    private void SetIsRunning(bool value)
    {
        locker.EnterWriteLock();
        try { isRunning = value; }
        finally { locker.ExitWriteLock(); }
    }

    private void RunJob(Action job)
    {
        try
        {
            if (CancellationToken.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                RaiseCanceled();
                return;
            }

            job();
        }
        catch (Exception ex)
        {
            ManageException(ex);
        }
        finally
        {
            ReleaseSemaphore();
        }
    }

    private async Task RunJobAsync(Func<Task> job)
    {
        try
        {
            if (CancellationToken.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
                RaiseCanceled();
                return;
            }

            await job();
        }
        catch (Exception ex)
        {
            ManageException(ex);
        }
        finally
        {
            ReleaseSemaphore();
        }
    }

    private string GetDebuggerDisplay() => ToString();

    public override string ToString()
    {
        ThrowIfDisposed();

        return $"IsRunning = {IsRunning()}";
    }

    #region IDisposable implementation

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
            return;

        if (IsRunning())
            throw new InvalidOperationException("Cannot do this operation while running");

        if (disposing)
        {
            locker.Dispose();
            semaphore.Dispose();
            cancellationTokenSource.Dispose();
        }

        ClearWorkCollections();
        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
