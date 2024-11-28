using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class SemaphoreWorker : SemaphoreWorkerSlim, INeedleWorker
{
    private static readonly object lockObject = new();

    public SemaphoreWorker()
        : base() { }

    public SemaphoreWorker(int numberOfThreads)
        : base(numberOfThreads) { }

    public SemaphoreWorker(bool cancelPendingJobsIfAnyOtherFails)
        : base(cancelPendingJobsIfAnyOtherFails) { }

    public SemaphoreWorker(int numberOfThreads, bool cancelPendingJobsIfAnyOtherFails)
        : base(numberOfThreads, cancelPendingJobsIfAnyOtherFails) { }

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

        lock (lockObject)
        {
            ThrowIfRunning();
            IsRunning = true;
        }

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
            lock (lockObject)
                IsRunning = false;

            ClearWorkCollections();
            ResetCancellationToken();
        }
    }

    public override void AddJob(Action job)
    {
        base.AddJob(job);

        lock (lockObject)
            TotalJobsCount++;
    }

    public override void AddJob(Func<Task> job)
    {
        base.AddJob(job);

        lock (lockObject)
            TotalJobsCount++;
    }

    protected override void AddJobActionToSemaphore(Action job)
    {
        AddTaskToSemaphore(Task.Run(() =>
        {
            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    RaiseCanceled();
                    return;
                }

                job();

                lock (lockObject)
                    SuccessfullyCompletedJobsCount++;
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

    protected override void AddJobTaskToSemaphore(Func<Task> job)
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

                await job();

                lock (lockObject)
                    SuccessfullyCompletedJobsCount++;
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

        lock (lockObject)
            FaultedJobsCount++;

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