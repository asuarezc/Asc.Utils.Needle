using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class SemaphoreWorker : SemaphoreWorkerSlim, INeedleWorker
{
    private int _totalJobsCount = 0;
    private int _successfullyCompletedJobsCount = 0;
    private int _faultedJobsCount = 0;

    private readonly Lock _locker = new();

    public SemaphoreWorker() {}

    public SemaphoreWorker(int numberOfThreads)
        : base(numberOfThreads) { }

    public SemaphoreWorker(OnJobFailedBehaviour onJobFailedBehaviour)
        : base(onJobFailedBehaviour) { }

    public SemaphoreWorker(int numberOfThreads, OnJobFailedBehaviour onJobFailedBehaviour)
        : base(numberOfThreads, onJobFailedBehaviour) { }

    #region INeedleWorker implementation

    public event EventHandler<Exception>? JobFaulted;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning { get; private set; }

    public int TotalJobsCount => Volatile.Read(ref _totalJobsCount);

    public int SuccessfullyCompletedJobsCount => Volatile.Read(ref _successfullyCompletedJobsCount);

    public int FaultedJobsCount => Volatile.Read(ref _faultedJobsCount);

    #endregion

    #region Overrides

    public override async Task RunAsync()
    {
        ThrowIfDisposed();
        ThrowIfThereIsNoJobsToRun();

        using (_locker.EnterScope())
        {
            ThrowIfRunning();
            IsRunning = true;
        }

        SafeNotifyPropertyChanged(nameof(IsRunning));

        try
        {
            await RunInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            using (_locker.EnterScope())
            {
                IsRunning = false;
                _cancellationTokenSource = new CancellationTokenSource();
            }

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

    protected override void AddJobToSemaphore(Func<Task> job)
    {
        Task? task;

        try
        {
            if (CancellationToken.IsCancellationRequested)
            {
                RaiseCanceled();
                return;
            }

            task = job();
        }
        catch (Exception ex)
        {
            ManageException(ex);
            ReleaseSemaphore();
            return;
        }

        if (task is null)
        {
            ManageException(new InvalidOperationException("The job returned a null task"));
            ReleaseSemaphore();
            return;
        }

        if (task.IsCompleted)
        {
            try
            {
                if (task.IsFaulted)
                    ManageException(task.Exception ?? new AggregateException());
                else if (task.IsCanceled)
                    RaiseCanceled();
                else
                {
                    Interlocked.Increment(ref _successfullyCompletedJobsCount);
                    SafeNotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
                }
            }
            finally
            {
                ReleaseSemaphore();
            }

            return;
        }

        AddTaskToSemaphore(task);

        task.ContinueWith(executedTask =>
        {
            try
            {
                if (executedTask.IsFaulted)
                    ManageException(executedTask.Exception ?? new AggregateException());
                else if (executedTask.IsCanceled)
                    RaiseCanceled();
                else
                {
                    Interlocked.Increment(ref _successfullyCompletedJobsCount);
                    SafeNotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
                }
            }
            catch (Exception ex)
            {
                ManageException(ex);
            }
            finally
            {
                ReleaseSemaphore();
            }
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }

    protected override void ManageException(Exception ex)
    {
        base.ManageException(ex);

        Interlocked.Increment(ref _faultedJobsCount);
        SafeNotifyPropertyChanged(nameof(FaultedJobsCount));

        try
        {
            JobFaulted?.Invoke(this, ex);
        }
        catch { } //Swallow to avoid crashing the worker; exceptions are already recorded by ManageException
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

        foreach (PropertyChangedEventHandler? single in handlers.GetInvocationList().Cast<PropertyChangedEventHandler>())
        {
            try
            {
                single(this, args);
            }
            catch { } //Swallow subscriber exceptions to avoid crashing the worker thread.
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