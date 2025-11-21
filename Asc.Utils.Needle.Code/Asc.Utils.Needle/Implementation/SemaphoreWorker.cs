using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class SemaphoreWorker : SemaphoreWorkerSlim, INeedleWorker
{
    private int _totalJobsCount = 0;
    private int _successfullyCompletedJobsCount = 0;
    private int _faultedJobsCount = 0;

    private static readonly Lock _locker = new();

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

        NotifyPropertyChanged(nameof(IsRunning));

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

            NotifyPropertyChanged(nameof(IsRunning));
            ClearWorkCollections();
        }
    }

    public override void AddJob(Action job)
    {
        base.AddJob(job);

        Interlocked.Increment(ref _totalJobsCount);
        NotifyPropertyChanged(nameof(TotalJobsCount));
    }

    public override void AddJob(Func<Task> job)
    {
        base.AddJob(job);

        Interlocked.Increment(ref _totalJobsCount);
        NotifyPropertyChanged(nameof(TotalJobsCount));
    }

    protected override void AddJobToSemaphore(Func<Task> job)
    {
        AddTaskToSemaphore(Task.Run(async () =>
        {
            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    RaiseCanceled();
                    return;
                }

                await job().ConfigureAwait(false);

                Interlocked.Increment(ref _successfullyCompletedJobsCount);
                NotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
            }
            catch (Exception ex)
            {
                ManageException(ex);
            }
            finally
            {
                ReleaseSemaphore();
            }
        }));
    }

    protected override void ManageException(Exception ex)
    {
        base.ManageException(ex);

        Interlocked.Increment(ref _faultedJobsCount);
        NotifyPropertyChanged(nameof(FaultedJobsCount));
        JobFaulted?.Invoke(this, ex);
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

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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