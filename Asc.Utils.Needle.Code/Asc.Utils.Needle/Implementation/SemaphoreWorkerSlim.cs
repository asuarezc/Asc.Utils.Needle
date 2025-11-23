using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class SemaphoreWorkerSlim : INeedleWorkerSlim
{
    private bool _disposedValue;
    private bool _canceledEventAlreadyRaised;
    private bool _isRunning;

    private readonly Lock _locker = new();
    private readonly Lock _lockerForExceptions = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly List<Func<Task>> _jobs = [];
    private readonly List<Task> _tasks = [];
    private List<Exception> _exceptions = [];

    protected CancellationTokenSource _cancellationTokenSource = new();

    public SemaphoreWorkerSlim()
        : this(Environment.ProcessorCount, OnJobFailedBehaviour.CancelPendingJobs) { }

    public SemaphoreWorkerSlim(int numberOfThreads)
        : this(numberOfThreads, OnJobFailedBehaviour.CancelPendingJobs) { }

    public SemaphoreWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour)
        : this(Environment.ProcessorCount, onJobFailedBehaviour) { }

    public SemaphoreWorkerSlim(int numberOfThreads, OnJobFailedBehaviour onJobFailedBehaviour)
    {
        if (numberOfThreads <= 0)
            throw new ArgumentException($"Param {numberOfThreads} must be greater than zero", nameof(numberOfThreads));

        _semaphore = new SemaphoreSlim(numberOfThreads);
        OnJobFailedBehaviour = onJobFailedBehaviour;
    }

    #region INeedleWorkerSlim implementation

    public event EventHandler? Canceled;

    public CancellationToken CancellationToken
    {
        get
        {
            ThrowIfDisposed();
            return _cancellationTokenSource.Token;
        }
    }

    public OnJobFailedBehaviour OnJobFailedBehaviour { get; }

    public virtual void AddJob(Action job)
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(job);

        _jobs.Add(() =>
        {
            job();
            return Task.CompletedTask;
        });
    }

    public virtual void AddJob(Func<Task> job)
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(job);

        _jobs.Add(job);
    }

    public void Cancel()
    {
        ThrowIfDisposed();
        ThrowIfNotRunning();
        ThrowIfCancellationAlreadyRequested();

        _cancellationTokenSource.Cancel();
        RaiseCanceled();
    }

    public virtual async Task RunAsync()
    {
        ThrowIfDisposed();
        ThrowIfThereIsNoJobsToRun();

        using (_locker.EnterScope())
        {
            ThrowIfRunning();
            _isRunning = true;
        }

        try
        {
            await RunInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            using (_locker.EnterScope())
            {
                _isRunning = false;
                _canceledEventAlreadyRaised = false;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            ClearWorkCollections();
        }
    }

    #endregion

    #region Protected methods

    protected virtual void AddJobToSemaphore(Func<Task> job)
    {
        AddTaskToSemaphore(RunJobAsync(job));
    }

    protected virtual void ManageException(Exception ex)
    {
        if (!CancellationToken.IsCancellationRequested && OnJobFailedBehaviour == OnJobFailedBehaviour.CancelPendingJobs)
            Cancel();

        using (_lockerForExceptions.EnterScope())
            _exceptions.Add(ex);
    }

    protected virtual void ThrowIfRunning()
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot do this operation while running");
    }

    protected virtual void ThrowIfNotRunning()
    {
        if (!_isRunning)
            throw new InvalidOperationException("Cannot do this operation while not running");
    }

    protected void RaiseCanceled()
    {
        using (_locker.EnterScope())
        {
            if (_canceledEventAlreadyRaised)
                return;

            _canceledEventAlreadyRaised = true;

            try
            {
                Canceled?.Invoke(this, EventArgs.Empty);
            }
            catch { } //Swallow exceptions from event handlers
        }
    }

    protected void ReleaseSemaphore()
    {
        _semaphore.Release();
    }

    protected void AddTaskToSemaphore(Task task)
    {
        _tasks.Add(task);
    }

    protected async Task RunInternalAsync()
    {
        //Reserve capacity to avoid multiple reallocations
        if (_tasks.Capacity < _jobs.Count)
            _tasks.Capacity = _jobs.Count;

        foreach (Func<Task> job in _jobs)
        {
            try
            {
                await _semaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            AddJobToSemaphore(job);
        }

        //Create snapshot of tasks to wait outside lock
        Task[] toWait;

        using (_locker.EnterScope())
            toWait = [.. _tasks];

        await Task.WhenAll(toWait).ConfigureAwait(false);

        //Create snapshot of exceptions to throw outside lock
        List<Exception> exceptionsSnapshot;

        using (_lockerForExceptions.EnterScope())
            exceptionsSnapshot = _exceptions.Count == 0 ? null! : [.. _exceptions];

        if (exceptionsSnapshot != null && exceptionsSnapshot.Count > 0)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", exceptionsSnapshot);
    }

    protected void ClearWorkCollections()
    {
        _jobs.Clear();

        using (_lockerForExceptions.EnterScope())
            _exceptions = [];

        _tasks.Clear();
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
    }

    protected void ThrowIfCancellationAlreadyRequested()
    {
        if (CancellationToken.IsCancellationRequested)
            throw new InvalidOperationException("Cannot do this operation since cancellation has been requested");
    }

    protected void ThrowIfThereIsNoJobsToRun()
    {
        if (_jobs.Count == 0)
            throw new InvalidOperationException("Nothing to run. Add jobs before running a worker");
    }

    #endregion

    private async Task RunJobAsync(Func<Task> job)
    {
        try
        {
            if (CancellationToken.IsCancellationRequested)
            {
                RaiseCanceled();
                return;
            }

            Task? task;

            try
            {
                task = job();
            }
            catch (Exception ex)
            {
                ManageException(ex);
                return;
            }

            if (task is null)
                throw new InvalidOperationException("The job returned a null task");

            await task.ConfigureAwait(false);
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

    private string GetDebuggerDisplay()
    {
        try
        {
            int pendingJobs = _jobs?.Count ?? 0;
            int tasksCount = _tasks?.Count ?? 0;
            bool cancelRequested = _cancellationTokenSource?.IsCancellationRequested ?? false;
            bool disposed = _disposedValue;

            return $"IsRunning={_isRunning}, OnJobFailedBehaviour={OnJobFailedBehaviour}, PendingJobs={pendingJobs}, Tasks={tasksCount}, CancelRequested={cancelRequested}, Disposed={disposed}";
        }
        catch
        {
            // Ensure debugger display never throws
            return $"IsRunning={_isRunning}";
        }
    }

    public override string ToString() => GetDebuggerDisplay();

    #region IDisposable implementation

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;

        if (_isRunning)
            throw new InvalidOperationException("Cannot dispose while running");

        if (disposing)
        {
            _semaphore.Dispose();
            _cancellationTokenSource.Dispose();
        }

        ClearWorkCollections();
        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}