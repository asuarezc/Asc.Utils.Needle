using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class ParallelWorker(OnJobFailedBehaviour onJobFailedBehaviour)
    : ParallelWorkerSlim(onJobFailedBehaviour), INeedleWorker
{
    private static readonly Lock locker = new();

    public ParallelWorker() : this(OnJobFailedBehaviour.CancelPendingJobs) { }

    #region INeedleWorker implementation

    public event EventHandler<Exception>? JobFaulted;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning { get; private set; }

    public int TotalJobsCount { get; private set; }

    public int SuccessfullyCompletedJobsCount { get; private set; }

    public int FaultedJobsCount { get; private set; }

    #endregion

    #region Overrides

    public override async Task RunAsync()
    {
        ThrowIfDisposed();
        ThrowIfThereIsNoJobsToRun();

        locker.Enter();

        try
        {
            ThrowIfRunning();
            IsRunning = true;
        }
        finally
        {
            locker.Exit();
        }

        NotifyPropertyChanged(nameof(IsRunning));

        try
        {
            await RunInternalAsync();
        }
        finally
        {
            locker.Enter();

            try
            {
                IsRunning = false;
                _cancellationTokenSource = new CancellationTokenSource();
            }
            finally
            {
                locker.Exit();
            }

            NotifyPropertyChanged(nameof(IsRunning));
            ClearWorkCollections();
        }
    }

    public override void AddJob(Action job)
    {
        base.AddJob(job);

        locker.Enter();

        try { TotalJobsCount++; }
        finally { locker.Exit(); }

        NotifyPropertyChanged(nameof(TotalJobsCount));
    }

    public override void AddJob(Func<Task> job)
    {
        base.AddJob(job);

        locker.Enter();

        try { TotalJobsCount++; }
        finally { locker.Exit(); }

        NotifyPropertyChanged(nameof(TotalJobsCount));
    }

    protected override Task GetTaskFromJob(Action job)
    {
        return Task.Run(() =>
        {
            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    RaiseCanceled();
                    return;
                }

                job();

                locker.Enter();

                try { SuccessfullyCompletedJobsCount++; }
                finally { locker.Exit(); }

                NotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
            }
            catch (Exception ex)
            {
                ManageException(ex);
            }
        });
    }

    protected override Task GetTaskFromFunc(Func<Task> job)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    RaiseCanceled();
                    return;
                }

                await job();

                locker.Enter();

                try { SuccessfullyCompletedJobsCount++; }
                finally { locker.Exit(); }

                NotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
            }
            catch (Exception ex)
            {
                ManageException(ex);
            }
        });
    }

    protected override void ManageException(Exception ex)
    {
        base.ManageException(ex);

        locker.Enter();

        try { FaultedJobsCount++; }
        finally { locker.Exit(); }

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