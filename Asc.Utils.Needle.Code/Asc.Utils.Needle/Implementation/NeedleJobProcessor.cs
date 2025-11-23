using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
internal class NeedleJobProcessor : NeedleJobProcessorSlim, INeedleJobProcessor
{
    private int _totalSuccessfullyProcessedJobsCount;
    private int _totalFaultedProcessedJobsCount;
    private int _totalAddedJobsCount;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SuccessfullyCompletedJobsCount => Volatile.Read(ref _totalSuccessfullyProcessedJobsCount);
    public int FaultedJobsCount => Volatile.Read(ref _totalFaultedProcessedJobsCount);
    public int TotalJobsCount => Volatile.Read(ref _totalAddedJobsCount);

    public NeedleJobProcessor(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour, IAsyncManualResetEvent pauseEvent)
        : base(threadPoolSize, onJobFailedBehaviour, pauseEvent)
    {
        JobFaulted += OnJobFaultedInternal;
    }

    private void OnJobFaultedInternal(object? sender, Exception ex)
    {
        Interlocked.Increment(ref _totalFaultedProcessedJobsCount);
        OnPropertyChanged(nameof(FaultedJobsCount));
    }

    private void OnPropertyChanged(string propertyName)
    {
        try
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        catch { } //Swallow exceptions from handlers to avoid killing worker threads
    }

    public new void ProcessJob(Action job)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(job);

        Interlocked.Increment(ref _totalAddedJobsCount);
        OnPropertyChanged(nameof(TotalJobsCount));

        Task wrappedJob()
        {
            try
            {
                job();
                Interlocked.Increment(ref _totalSuccessfullyProcessedJobsCount);
                OnPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
                return Task.CompletedTask;
            }
            catch
            {
                throw; //Let base processor handle exception reporting via JobFaulted
            }
        }

        base.ProcessJob(wrappedJob);
    }

    public new void ProcessJob(Func<Task> job)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(job);

        Interlocked.Increment(ref _totalAddedJobsCount);
        OnPropertyChanged(nameof(TotalJobsCount));

        async Task wrappedJobAsync()
        {
            try
            {
                await job().ConfigureAwait(false);
                Interlocked.Increment(ref _totalSuccessfullyProcessedJobsCount);
                OnPropertyChanged(nameof(SuccessfullyCompletedJobsCount));
            }
            catch
            {
                throw; //Let base processor handle exception reporting via JobFaulted
            }
        }

        base.ProcessJob(wrappedJobAsync);
    }

    public new void Start()
    {
        base.Start();
        OnPropertyChanged(nameof(Status));
    }

    public new void Pause()
    {
        base.Pause();
        OnPropertyChanged(nameof(Status));
    }

    public new void Resume()
    {
        base.Resume();
        OnPropertyChanged(nameof(Status));
    }

    private string GetDebuggerDisplay()
    {
        string baseInfo = base.ToString();
        return $"{baseInfo}, TotalJobs={TotalJobsCount}, Success={SuccessfullyCompletedJobsCount}, Faulted={FaultedJobsCount}";
    }

    public override string ToString() => GetDebuggerDisplay();

    #region IDisposable and AsyncDisposable Support

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        JobFaulted -= OnJobFaultedInternal;
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        JobFaulted -= OnJobFaultedInternal;
    }

    #endregion
}