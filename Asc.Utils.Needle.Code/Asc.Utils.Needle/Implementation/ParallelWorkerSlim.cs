using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
[SuppressMessage("ReSharper", "MethodSupportsCancellation")]
internal class ParallelWorkerSlim(bool cancelPendingJobsIfAnyOtherFails) : INeedleWorkerSlim
{
    private bool disposedValue;
    private bool canceledEventAlreadyRaised;
    private bool isRunning;

    private static readonly Lock locker = new();
    private readonly ConcurrentBag<Action> actionJobs = [];
    private readonly ConcurrentBag<Func<Task>> taskJobs = [];
    private readonly ConcurrentBag<Exception> exceptions = [];

    protected CancellationTokenSource cancellationTokenSource = new();

    public ParallelWorkerSlim() : this(true) { }

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

    public bool CancelPendingJobsIfAnyOtherFails { get; } = cancelPendingJobsIfAnyOtherFails;

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

    protected async Task RunInternalAsync()
    {
        IEnumerable<Task> tasks = actionJobs
            .Select(GetTaskFromJob)
            .Concat(taskJobs.Select(GetTaskFromFunc));

        await Task.WhenAll(tasks);

        if (!exceptions.IsEmpty)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", exceptions);
    }

    protected void ClearWorkCollections()
    {
        actionJobs.Clear();
        taskJobs.Clear();
        exceptions.Clear();
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
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

    protected virtual void ManageException(Exception ex)
    {
        if (!CancellationToken.IsCancellationRequested && CancelPendingJobsIfAnyOtherFails)
            Cancel();

        exceptions.Add(ex);
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

    #endregion

    protected virtual Task GetTaskFromJob(Action job)
    {
        return Task.Run(() =>
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
        });
    }

    protected virtual Task GetTaskFromFunc(Func<Task> job)
    {
        return Task.Run(async () =>
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
        });
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
            cancellationTokenSource.Dispose();

        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}