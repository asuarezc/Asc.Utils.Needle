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

    public INeedleWorkerSlim GetSemaphoreWorkerSlim(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new SemaphoreWorkerSlim(cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads, bool cancelPendingJobsIfAnyOtherFails)
    {
        return new SemaphoreWorkerSlim(maxThreads, cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorker GetSemaphoreWorker()
    {
        return new SemaphoreWorker();
    }

    public INeedleWorker GetSemaphoreWorker(int maxThreads)
    {
        return new SemaphoreWorker(maxThreads);
    }

    public INeedleWorker GetSemaphoreWorker(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new SemaphoreWorker(cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorker GetSemaphoreWorker(int maxThreads, bool cancelPendingJobsIfAnyOtherFails)
    {
        return new SemaphoreWorker(maxThreads, cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorkerSlim GetParallelWorkerSlim()
    {
        return new ParallelWorkerSlim();
    }

    public INeedleWorkerSlim GetParallelWorkerSlim(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new ParallelWorkerSlim(cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorker GetParallelWorker()
    {
        return new ParallelWorker();
    }

    public INeedleWorker GetParallelWorker(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new ParallelWorker(cancelPendingJobsIfAnyOtherFails);
    }
}