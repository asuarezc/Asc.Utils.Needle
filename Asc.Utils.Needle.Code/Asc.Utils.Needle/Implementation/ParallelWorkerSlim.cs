using System.Collections.Concurrent;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class ParallelWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour) : INeedleWorkerSlim
{
    private bool _disposedValue;
    private bool _canceledEventAlreadyRaised;
    private bool _isRunning;

    private static readonly Lock _locker = new();
    private readonly ConcurrentBag<Action> _actionJobs = [];
    private readonly ConcurrentBag<Func<Task>> _taskJobs = [];
    private readonly ConcurrentBag<Exception> _exceptions = [];

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

    protected async Task RunInternalAsync()
    {
        IEnumerable<Task> tasks = _actionJobs
            .Select(GetTaskFromJob)
            .Concat(_taskJobs.Select(GetTaskFromFunc));

        await Task.WhenAll(tasks);

        if (!_exceptions.IsEmpty)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", _exceptions);
    }

    protected void ClearWorkCollections()
    {
        _actionJobs.Clear();
        _taskJobs.Clear();
        _exceptions.Clear();
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
        if (_actionJobs.IsEmpty && _taskJobs.IsEmpty)
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

    #endregion

    protected virtual Task GetTaskFromJob(Action job)
    {
        return Task.Run(() =>
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
        });
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