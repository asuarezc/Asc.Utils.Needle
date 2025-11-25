using Asc.Utils.Needle.Implementation;

namespace Asc.Utils.Needle;

public sealed class Pincushion : IPincushion
{
    #region Singleton stuff

    private static readonly Lazy<IPincushion> lazyInstance = new(
        () => new Pincushion(),
        LazyThreadSafetyMode.PublicationOnly
    );

    private Pincushion() { }

    public static IPincushion Instance => lazyInstance.Value;

    #endregion

    public INeedleWorkerSlim GetSemaphoreWorkerSlim()
    {
        return new SemaphoreWorkerSlim();
    }

    public INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads)
    {
        return new SemaphoreWorkerSlim(maxThreads);
    }

    public INeedleWorkerSlim GetSemaphoreWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new SemaphoreWorkerSlim(onJobFailedBehaviour);
    }
     
    public INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads, OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new SemaphoreWorkerSlim(maxThreads, onJobFailedBehaviour);
    }

    public INeedleWorker GetSemaphoreWorker()
    {
        return new SemaphoreWorker();
    }

    public INeedleWorker GetSemaphoreWorker(int maxThreads)
    {
        return new SemaphoreWorker(maxThreads);
    }

    public INeedleWorker GetSemaphoreWorker(OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new SemaphoreWorker(onJobFailedBehaviour);
    }

    public INeedleWorker GetSemaphoreWorker(int maxThreads, OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new SemaphoreWorker(maxThreads, onJobFailedBehaviour);
    }

    public INeedleWorkerSlim GetParallelWorkerSlim()
    {
        return new ParallelWorkerSlim();
    }

    public INeedleWorkerSlim GetParallelWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new ParallelWorkerSlim(onJobFailedBehaviour);
    }

    public INeedleWorker GetParallelWorker()
    {
        return new ParallelWorker();
    }

    public INeedleWorker GetParallelWorker(OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new ParallelWorker(onJobFailedBehaviour);
    }

    public INeedleJobProcessorSlim GetNeedleJobProcessorSlim()
    {
        return new JobProcessorSlim(
            threadPoolSize: Environment.ProcessorCount,
            onJobFailedBehaviour: OnJobFailedBehaviour.ContinueRunningPendingJobs,
            pauseEvent: new AsyncManualResetEvent(initialState: false)
        );
    }

    public INeedleJobProcessorSlim GetNeedleJobProcessorSlim(int threadPoolSize)
    {
        if (threadPoolSize < 0)
            throw new ArgumentOutOfRangeException(nameof(threadPoolSize), "Thread pool size must be non-negative.");

        return new JobProcessorSlim(threadPoolSize, OnJobFailedBehaviour.ContinueRunningPendingJobs, new AsyncManualResetEvent(initialState: false));
    }

    public INeedleJobProcessorSlim GetNeedleJobProcessorSlim(OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new JobProcessorSlim(
            threadPoolSize: Environment.ProcessorCount,
            onJobFailedBehaviour: onJobFailedBehaviour,
            pauseEvent: new AsyncManualResetEvent(initialState: false)
        );
    }

    public INeedleJobProcessorSlim GetNeedleJobProcessorSlim(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour)
    {
        if (threadPoolSize < 0)
            throw new ArgumentOutOfRangeException(nameof(threadPoolSize), "Thread pool size must be non-negative.");

        return new JobProcessorSlim(threadPoolSize, onJobFailedBehaviour, new AsyncManualResetEvent(initialState: false));
    }

    public INeedleJobProcessor GetNeedleJobProcessor()
    {
        return new JobProcessor(
            threadPoolSize: Environment.ProcessorCount,
            onJobFailedBehaviour: OnJobFailedBehaviour.ContinueRunningPendingJobs,
            pauseEvent: new AsyncManualResetEvent(initialState: false)
        );
    }

    public INeedleJobProcessor GetNeedleJobProcessor(int threadPoolSize)
    {
        if (threadPoolSize < 0)
            throw new ArgumentOutOfRangeException(nameof(threadPoolSize), "Thread pool size must be non-negative.");

        return new JobProcessor(threadPoolSize, OnJobFailedBehaviour.ContinueRunningPendingJobs, new AsyncManualResetEvent(initialState: false));
    }

    public INeedleJobProcessor GetNeedleJobProcessor(OnJobFailedBehaviour onJobFailedBehaviour)
    {
        return new JobProcessor(
            threadPoolSize: Environment.ProcessorCount,
            onJobFailedBehaviour: onJobFailedBehaviour,
            pauseEvent: new AsyncManualResetEvent(initialState: false)
        );
    }

    public INeedleJobProcessor GetNeedleJobProcessor(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour)
    {
        if (threadPoolSize < 0)
            throw new ArgumentOutOfRangeException(nameof(threadPoolSize), "Thread pool size must be non-negative.");

        return new JobProcessor(threadPoolSize, onJobFailedBehaviour, new AsyncManualResetEvent(initialState: false));
    }
}