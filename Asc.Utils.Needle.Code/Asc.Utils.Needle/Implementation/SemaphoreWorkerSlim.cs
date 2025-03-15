using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
internal class SemaphoreWorkerSlim : INeedleWorkerSlim
{
    private bool disposedValue;
    private bool canceledEventAlreadyRaised;
    private bool isRunning;

    private static readonly Lock locker = new();
    private readonly SemaphoreSlim semaphore;
    private readonly ConcurrentBag<Action> actionJobs = [];
    private readonly ConcurrentBag<Func<Task>> taskJobs = [];
    private readonly ConcurrentBag<Exception> exceptions = [];
    private readonly List<Task> tasks = [];

    protected CancellationTokenSource cancellationTokenSource = new();

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

    public bool CancelPendingJobsIfAnyOtherFails { get; }

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
        ThrowIfThereIsNoJobsToRun();

        locker.Enter();

        try
        {
            ThrowIfRunning();
            isRunning = true;
        }
        finally
        {
            locker.Exit();
        }

        try
        {
            await RunInternalAsync();
        }
        finally
        {
            locker.Enter();

            try
            {
                isRunning = false;
                canceledEventAlreadyRaised = false;
                cancellationTokenSource = new CancellationTokenSource();
            }
            finally
            {
                locker.Exit();
            }

            ClearWorkCollections();
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
        if (isRunning)
            throw new InvalidOperationException("Cannot do this operation while running");
    }

    protected virtual void ThrowIfNotRunning()
    {
        if (!isRunning)
            throw new InvalidOperationException("Cannot do this operation while not running");
    }

    protected void RaiseCanceled()
    {
        locker.Enter();

        try
        {
            if (canceledEventAlreadyRaised)
                return;

            canceledEventAlreadyRaised = true;
            Canceled?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            locker.Exit();
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
            await semaphore.WaitAsync(CancellationToken);
            AddJobActionToSemaphore(job);
        }

        foreach (Func<Task> job in taskJobs)
        {
            await semaphore.WaitAsync(CancellationToken);
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
                await cancellationTokenSource.CancelAsync();
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

        return $"IsRunning = {isRunning}";
    }

    #region IDisposable implementation

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
            return;

        if (isRunning)
            throw new InvalidOperationException("Cannot do this operation while running");

        if (disposing)
        {
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