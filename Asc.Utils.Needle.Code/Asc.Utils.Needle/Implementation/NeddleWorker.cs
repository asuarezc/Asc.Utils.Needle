using System;
using System.ComponentModel;

namespace Asc.Utils.Needle.Implementation;

internal class NeddleWorker(int maxThreads = 3, bool cancelPendingJobsIfAnyOtherFails = true) : INeddleWorker
{
    private bool disposedValue;
    private bool isRunning;
    private int totalJobsCount;
    private int completedJobsCount;
    private string DebugDisplay => ToString();

    private readonly SemaphoreSlim semaphore = new(maxThreads);
    private CancellationTokenSource cancellationTokenSource = new();

    private List<Tuple<Action, JobPriority>> actionJobs = [];
    private List<Tuple<Func<Task>, JobPriority>> taskJobs = [];
    private List<Exception> exceptions = [];
    private List<Task> tasks = [];

    public CancellationToken CancellationToken
    {
        get
        {
            ThrowIfDisposed();
            return cancellationTokenSource.Token;
        }
    }

    public int MaxThreads
    {
        get
        {
            ThrowIfDisposed();
            return maxThreads;
        }
    }

    public int TotalJobsCount
    {
        get
        {
            ThrowIfDisposed();
            return totalJobsCount;
        }
        private set
        {
            ThrowIfDisposed();
            totalJobsCount = value;
            NotifyPropertyChanged(nameof(TotalJobsCount));
            NotifyPropertyChanged(nameof(Progress));
        }
    }

    public int CompletedJobsCount
    {
        get
        {
            ThrowIfDisposed();
            return completedJobsCount;
        }
        private set
        {
            ThrowIfDisposed();
            completedJobsCount = value;
            NotifyPropertyChanged(nameof(CompletedJobsCount));
            NotifyPropertyChanged(nameof(Progress));
        }
    }

    public int Progress
    {
        get
        {
            ThrowIfDisposed();
            return TotalJobsCount > 0
                ? completedJobsCount * 100 / TotalJobsCount
                : 0;
        }
    }

    public bool CancelPendingJobsIfAnyOtherFails
    {
        get
        {
            ThrowIfDisposed();
            return cancelPendingJobsIfAnyOtherFails;
        }
    }
    

    public bool IsRunning
    {
        get
        {
            ThrowIfDisposed();
            return isRunning;
        }
        private set
        {
            ThrowIfDisposed();
            isRunning = value;
            NotifyPropertyChanged(nameof(IsRunning));
        }
    }

    public event EventHandler<Exception> JobFaulted;
    public event EventHandler Completed;
    public event EventHandler Canceled;
    public event PropertyChangedEventHandler PropertyChanged;

    public override string ToString()
    {
        ThrowIfDisposed();
        return $"IsRunning = {IsRunning}, Progress = ({Progress}% completed {CompletedJobsCount} of {TotalJobsCount} jobs)";
    }
        

    public void AddJob(Action job, JobPriority priority = JobPriority.Medium)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(job);

        if (IsRunning)
            throw new InvalidOperationException("Cannot add jobs while running");

        actionJobs.Add(new Tuple<Action, JobPriority>(job, priority));
        TotalJobsCount++;
    }

    public void AddJob(Func<Task> job, JobPriority priority = JobPriority.Medium)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(job);

        if (IsRunning)
            throw new InvalidOperationException("Cannot add jobs while running");

        taskJobs.Add(new Tuple<Func<Task>, JobPriority>(job, priority));
        TotalJobsCount++;
    }

    public async Task RunAsync()
    {
        ThrowIfDisposed();

        if (actionJobs.Count == 0 && taskJobs.Count == 0)
            throw new InvalidOperationException("Nothing to run");

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
            cancellationTokenSource = new();
            NotifyPropertyChanged(nameof(CancellationToken));
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void BeginRun()
    {
        ThrowIfDisposed();

        if (actionJobs.Count == 0 && taskJobs.Count == 0)
            throw new InvalidOperationException("Nothing to run");

        Task.Run(RunAsync);
    }

    public void RequestCancellation()
    {
        ThrowIfDisposed();

        if (!IsRunning)
            throw new InvalidOperationException("Semaphore is not running. Operation cannot be canceled.");

        cancellationTokenSource.Cancel();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposedValue, this);
    }

    private void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task RunInternalAsync()
    {
        List<Tuple<object, JobPriority>> jobs = [];

        if (actionJobs.Count > 0)
            jobs.AddRange(actionJobs.Select(it => new Tuple<object, JobPriority>(it.Item1, it.Item2)));

        if (taskJobs.Count > 0)
            jobs.AddRange(taskJobs.Select(it => new Tuple<object, JobPriority>(it.Item1, it.Item2)));

        if (jobs.Count == 0)
            throw new InvalidOperationException("Nothing to run");

        foreach (Tuple<object, JobPriority> job in jobs.OrderBy(it => (int)it.Item2))
        {
            await semaphore.WaitAsync();

            if (job.Item1 is Action)
                AddJobActionToSemaphore(job.Item1 as Action);
            else
                AddJobTaskToSemaphore(job.Item1 as Func<Task>);
        }

        await Task.WhenAll(tasks);

        if (exceptions.Count > 0)
            throw new AggregateException("Some jobs failed. See inner exceptions for more information.", exceptions);
    }

    private void AddJobActionToSemaphore(Action job)
    {
        tasks.Add(Task.Run(() =>
        {
            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Canceled?.Invoke(this, EventArgs.Empty);
                    return;
                }

                job();
                CompletedJobsCount++;
            }
            catch (Exception ex)
            {
                ManageException(ex);
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    private void AddJobTaskToSemaphore(Func<Task> job)
    {
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Canceled?.Invoke(this, EventArgs.Empty);
                    return;
                }

                await job();
                CompletedJobsCount++;
            }
            catch (Exception ex)
            {
                ManageException(ex);
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    private void ManageException(Exception ex)
    {
        exceptions.Add(ex);

        if (!CancellationToken.IsCancellationRequested && CancelPendingJobsIfAnyOtherFails)
            cancellationTokenSource.Cancel();

        JobFaulted?.Invoke(this, ex);
    }

    private void ClearWorkCollections()
    {
        actionJobs.Clear();
        taskJobs.Clear();
        exceptions.Clear();
        tasks.Clear();

        TotalJobsCount = 0;
        CompletedJobsCount = 0;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
            return;

        if (IsRunning)
            throw new InvalidOperationException("Cannot dispose while worker is running");

        if (disposing)
        {
            semaphore.Dispose();
            cancellationTokenSource.Dispose();
        }

        ClearWorkCollections();

        actionJobs = null;
        taskJobs = null;
        exceptions = null;
        tasks = null;

        JobFaulted = null;
        Completed = null;
        Canceled = null;

        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
