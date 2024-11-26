using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class ParallelWorker(bool cancelPendingJobsIfAnyOtherFails)
    : ParallelWorkerSlim(cancelPendingJobsIfAnyOtherFails), INeedleWorker
{
    private bool isRunning = false;
    private int totalJobsCount = 0;
    private int successfullyCompletedJobsCount = 0;
    private int faultedJobsCount = 0;

    private readonly ReaderWriterLockSlim locker = new(LockRecursionPolicy.NoRecursion);

    public ParallelWorker() : this(true) { }

    #region INeedleWorker implementation

    public event EventHandler? Completed;
    public event EventHandler<Exception>? JobFaulted;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRunning
    {
        get
        {
            ThrowIfDisposed();

            locker.EnterReadLock();
            try { return isRunning; }
            finally { locker.ExitReadLock(); }
        }
        private set
        {
            ThrowIfDisposed();

            locker.EnterWriteLock();
            try { isRunning = value; }
            finally { locker.ExitWriteLock(); }

            NotifyPropertyChanged(nameof(IsRunning));
        }
    }

    public int TotalJobsCount
    {
        get
        {
            ThrowIfDisposed();

            locker.EnterReadLock();
            try { return totalJobsCount; }
            finally { locker.ExitReadLock(); }
        }
        private set
        {
            ThrowIfDisposed();

            locker.EnterWriteLock();
            try { totalJobsCount = value; }
            finally { locker.ExitWriteLock(); }

            NotifyPropertyChanged(nameof(TotalJobsCount));
        }
    }

    public int SuccessfullyCompletedJobsCount
    {
        get
        {
            ThrowIfDisposed();

            locker.EnterReadLock();
            try { return successfullyCompletedJobsCount; }
            finally { locker.ExitReadLock(); }
        }
        private set
        {
            ThrowIfDisposed();

            locker.EnterWriteLock();
            try { successfullyCompletedJobsCount = value; }
            finally { locker.ExitWriteLock(); }

            NotifyPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
        }
    }

    public int FaultedJobsCount
    {
        get
        {
            ThrowIfDisposed();

            locker.EnterReadLock();
            try { return faultedJobsCount; }
            finally { locker.ExitReadLock(); }
        }
        private set
        {
            ThrowIfDisposed();

            locker.EnterWriteLock();
            try { faultedJobsCount = value; }
            finally { locker.ExitWriteLock(); }

            NotifyPropertyChanged(nameof(FaultedJobsCount));
        }
    }

    public void BeginRun()
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ThrowIfThereIsNoJobsToRun();

        Task.Run(RunAsync);
    }

    #endregion

    #region Overrides

    public override async Task RunAsync()
    {
        ThrowIfDisposed();
        ThrowIfRunning();
        ThrowIfThereIsNoJobsToRun();

        IsRunning = true;

        try
        {
            await RunInternalAsync();
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            IsRunning = false;

            ClearWorkCollections();
            ResetCancellationToken();
            ResetProperties();

            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void AddJob(Action job)
    {
        base.AddJob(job);
        TotalJobsCount++;
    }

    public override void AddJob(Func<Task> job)
    {
        base.AddJob(job);
        TotalJobsCount++;
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
                SuccessfullyCompletedJobsCount++;
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
                SuccessfullyCompletedJobsCount++;
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

        JobFaulted?.Invoke(this, ex);
        FaultedJobsCount++;
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

    private void ResetProperties()
    {
        TotalJobsCount = 0;
        SuccessfullyCompletedJobsCount = 0;
        FaultedJobsCount = 0;
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            locker.Dispose();

        base.Dispose(disposing);
    }
}