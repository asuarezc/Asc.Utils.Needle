using System.Collections.Concurrent;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class SemaphoreWorkerSlim : INeedleWorkerSlim
{
    private bool _disposedValue;
    private bool _canceledEventAlreadyRaised;
    private bool _isRunning;

    private static readonly Lock _locker = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentBag<Action> _actionJobs = [];
    private readonly ConcurrentBag<Func<Task>> _taskJobs = [];
    private readonly ConcurrentBag<Exception> _exceptions = [];
    private readonly List<Task> _tasks = [];

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

        _actionJobs.Add(job);
    }

    public virtual void AddJob(Func<Task> job)
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(job);

        _taskJobs.Add(job);
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
            await RunInternalAsync();
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
        if (!CancellationToken.IsCancellationRequested && OnJobFailedBehaviour == OnJobFailedBehaviour.CancelPendingJobs)
            Cancel();

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
            Canceled?.Invoke(this, EventArgs.Empty);
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
        foreach (Action job in _actionJobs)
        {
            await _semaphore.WaitAsync(CancellationToken);
            AddJobActionToSemaphore(job);
        }

        foreach (Func<Task> job in _taskJobs)
        {
            await _semaphore.WaitAsync(CancellationToken);
            AddJobTaskToSemaphore(job);
        }

        await Task.WhenAll(_tasks);

        if (!_exceptions.IsEmpty)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", _exceptions);
    }

    protected void ClearWorkCollections()
    {
        _actionJobs.Clear();
        _taskJobs.Clear();
        _exceptions.Clear();
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
        if (_actionJobs.IsEmpty && _taskJobs.IsEmpty)
            throw new InvalidOperationException("Nothing to run. Add jobs before running a worker");
    }

    #endregion

    private void RunJob(Action job)
    {
        try
        {
            if (CancellationToken.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
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
                await _cancellationTokenSource.CancelAsync();
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

        return $"IsRunning = {_isRunning}";
    }

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