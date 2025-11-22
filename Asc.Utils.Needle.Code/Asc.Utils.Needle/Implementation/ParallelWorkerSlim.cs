using System.Collections.Concurrent;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class ParallelWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour) : INeedleWorkerSlim
{
    private bool _disposedValue;
    private bool _canceledEventAlreadyRaised;
    private bool _isRunning;

    private readonly Lock _locker = new();
    private readonly List<Func<Task>> _jobs = [];
    private ConcurrentBag<Exception> _exceptions = [];

    protected CancellationTokenSource _cancellationTokenSource = new();

    public ParallelWorkerSlim() : this(OnJobFailedBehaviour.CancelPendingJobs) { }

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

    public OnJobFailedBehaviour OnJobFailedBehaviour { get; } = onJobFailedBehaviour;

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

    protected async Task RunInternalAsync()
    {
        int totalJobs = _jobs.Count;

        if (totalJobs == 0)
            return;

        var tasks = new Task[totalJobs];

        for (int i = 0; i < totalJobs; i++)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                RaiseCanceled();

                for (int j = i; j < totalJobs; j++)
                    tasks[j] = Task.CompletedTask;

                break;
            }

            tasks[i] = GetTaskFromFunc(_jobs[i]);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (!_exceptions.IsEmpty)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", _exceptions);
    }

    protected void ClearWorkCollections()
    {
        _jobs.Clear();
        _exceptions = [];
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
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

    protected virtual void ManageException(Exception ex)
    {
        if (!CancellationToken.IsCancellationRequested && OnJobFailedBehaviour == OnJobFailedBehaviour.CancelPendingJobs)
            Cancel();

        _exceptions.Add(ex);
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

    protected virtual Task GetTaskFromFunc(Func<Task> job)
    {
        Task jobTask;

        try
        {
            jobTask = job();
        }
        catch (Exception ex)
        {
            ManageException(ex);
            return Task.CompletedTask;
        }

        if (jobTask.IsCompleted)
        {
            if (jobTask.IsFaulted)
                ManageException(jobTask.Exception ?? new AggregateException());
            else if (jobTask.IsCanceled || CancellationToken.IsCancellationRequested)
                RaiseCanceled();

            return Task.CompletedTask;
        }

        return jobTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
                ManageException(t.Exception ?? new AggregateException());
            else if (t.IsCanceled || CancellationToken.IsCancellationRequested)
                RaiseCanceled();
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }

    #endregion

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
            _cancellationTokenSource.Dispose();

        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}