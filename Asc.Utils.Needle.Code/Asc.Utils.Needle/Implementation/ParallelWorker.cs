using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class ParallelWorker(OnJobFailedBehaviour onJobFailedBehaviour)
    : ParallelWorkerSlim(onJobFailedBehaviour), INeedleWorker
{
    private readonly Lock _locker = new();
    private int _totalJobsCount = 0;
    private int _successfullyCompletedJobsCount = 0;
    private int _failedJobsCount = 0;

    public ParallelWorker() : this(OnJobFailedBehaviour.CancelPendingJobs) { }

    #region INeedleWorker implementation

    public event EventHandler<Exception>? JobFaulted;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning { get; private set; }

    public int TotalJobsCount => Volatile.Read(ref _totalJobsCount);

    public int SuccessfullyCompletedJobsCount => Volatile.Read(ref _successfullyCompletedJobsCount);

    public int FaultedJobsCount => Volatile.Read(ref _failedJobsCount);

    #endregion

    #region Overrides

    public override async Task RunAsync()
    {
        ThrowIfDisposed();
        ThrowIfThereIsNoJobsToRun();

        _locker.Enter();
        try
        {
            ThrowIfRunning();
            IsRunning = true;
        }
        finally
        {
            _locker.Exit();
        }

        SafeNotifyPropertyChanged(nameof(IsRunning));

        try
        {
            await RunInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _locker.Enter();
            CancellationTokenSource oldCts = null!;
            try
            {
                IsRunning = false;
                // swap CTS and dispose old one to avoid leak
                oldCts = _cancellationTokenSource;
                _cancellationTokenSource = new CancellationTokenSource();
            }
            finally
            {
                _locker.Exit();
            }

            try { oldCts?.Dispose(); } catch { }

            SafeNotifyPropertyChanged(nameof(IsRunning));
            ClearWorkCollections();
        }
    }

    public override void AddJob(Action job)
    {
        base.AddJob(job);

        Interlocked.Increment(ref _totalJobsCount);
        SafeNotifyPropertyChanged(nameof(TotalJobsCount));
    }

    public override void AddJob(Func<Task> job)
    {
        base.AddJob(job);

        Interlocked.Increment(ref _totalJobsCount);
        SafeNotifyPropertyChanged(nameof(TotalJobsCount));
    }

    protected override Task GetTaskFromFunc(Func<Task> job)
    {
        Task? jobTask;

        try
        {
            jobTask = job();
        }
        catch (Exception ex)
        {
            ManageException(ex);
            return Task.CompletedTask;
        }

        if (jobTask is null)
            throw new InvalidOperationException("The job returned a null Task.");

        if (jobTask.IsCompleted)
        {
            if (jobTask.IsFaulted)
                ManageException(jobTask.Exception ?? new AggregateException());
            else if (jobTask.IsCanceled || CancellationToken.IsCancellationRequested)
                RaiseCanceled();
            else
            {
                Interlocked.Increment(ref _successfullyCompletedJobsCount);
                SafeNotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
            }

            return Task.CompletedTask;
        }

        return jobTask.ContinueWith(t =>
        {
            if (t.IsFaulted)
                ManageException(t.Exception ?? new AggregateException());
            else if (t.IsCanceled || CancellationToken.IsCancellationRequested)
                RaiseCanceled();
            else
            {
                Interlocked.Increment(ref _successfullyCompletedJobsCount);
                SafeNotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
            }
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }

    protected override void ManageException(Exception ex)
    {
        base.ManageException(ex);

        Interlocked.Increment(ref _failedJobsCount);
        SafeNotifyPropertyChanged(nameof(FaultedJobsCount));

        EventHandler<Exception>? handlers = JobFaulted;

        if (handlers is null)
            return;

        foreach (EventHandler<Exception> single in handlers.GetInvocationList().Cast<EventHandler<Exception>>())
        {
            try { single(this, ex); } catch { } //Swallow per subscriber to avoid crashing worker
        }
    }

    protected override void ThrowIfRunning()
    {
        if (IsRunning)
            throw new InvalidOperationException("Cannot do this operation while running");
    }

    protected override void ThrowIfNotRunning()
    {
        if (!IsRunning)
            throw new InvalidOperationException("Cannot do this operation while not running");
    }

    #endregion

    private void SafeNotifyPropertyChanged(string propertyName)
    {
        PropertyChangedEventHandler? handlers = PropertyChanged;

        if (handlers is null)
            return;

        PropertyChangedEventArgs args = new(propertyName);

        foreach (PropertyChangedEventHandler single in handlers.GetInvocationList().Cast<PropertyChangedEventHandler>())
        {
            try { single(this, args); } catch { } //Swallow per subscriber to avoid crashing worker
        }
    }

    private string GetDebuggerDisplay() => ToString();

    public override string ToString()
    {
        ThrowIfDisposed();

        return string.Concat(
            $"IsRunning = {IsRunning}, SuccessfullyCompletedJobsCount = {SuccessfullyCompletedJobsCount}, ",
            $"FaultedJobsCount = {FaultedJobsCount}, TotalJobsCount = {TotalJobsCount}"
        );
    }
}