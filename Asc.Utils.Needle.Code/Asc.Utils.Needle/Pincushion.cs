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
}