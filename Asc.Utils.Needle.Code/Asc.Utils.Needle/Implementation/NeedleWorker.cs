using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
internal class NeedleWorker(int maxThreads, bool cancelPendingJobsIfAnyOtherFails = true) : INeedleWorker
{
    private bool disposedValue;
    private bool isRunning;
    private int totalJobsCount;
    private int completedJobsCount;

    private static readonly object lockObject = new();
    private readonly SemaphoreSlim semaphore = new(maxThreads);
    private CancellationTokenSource cancellationTokenSource = new();

    private readonly List<Tuple<Action, JobPriority>> actionJobs = [];
    private readonly List<Tuple<Func<Task>, JobPriority>> taskJobs = [];
    private readonly List<Exception> exceptions = [];
    private readonly List<Task> tasks = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public NeedleWorker(bool cancelPendingJobsIfAnyOtherFails = true) : this(Environment.ProcessorCount, cancelPendingJobsIfAnyOtherFails) { }

    #region INeedleWorker implementation

    public event EventHandler? Completed;
    public event EventHandler<Exception>? JobFaulted;
    public event EventHandler? Canceled;

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

    #endregion

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

            if (job.Item1 is Action action)
                AddJobActionToSemaphore(action);
            else
                AddJobTaskToSemaphore((Func<Task>)job.Item1);
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

                lock (lockObject)
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

                lock (lockObject)
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
        if (!CancellationToken.IsCancellationRequested && CancelPendingJobsIfAnyOtherFails)
            cancellationTokenSource.Cancel();

        lock (lockObject)
        {
            exceptions.Add(ex);
            JobFaulted?.Invoke(this, ex);
        }
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

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    public override string ToString()
    {
        ThrowIfDisposed();
        return $"IsRunning = {IsRunning}, Progress = ({Progress}% completed {CompletedJobsCount} of {TotalJobsCount} jobs)";
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
        disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}