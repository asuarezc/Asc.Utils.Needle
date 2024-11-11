using Asc.Utils.Needle.Implementation;

namespace Asc.Utils.Needle;

/// <summary>
/// The simplest IPincushion singleton implementation for the simplest possible needle factory
/// </summary>
public sealed class Needle : INeedle
{
    #region Singleton stuff

    private static readonly Lazy<INeedle> lazyInstance = new(() => new Needle(), LazyThreadSafetyMode.PublicationOnly);

    private Needle() { }

    public static INeedle Instance => lazyInstance.Value;

    #endregion

    public IBackgroundSemaphoreWorker MainBackgroundWorker => Implementation.MainBackgroundWorker.Instance;

    public IParallelWorker GetParallelWorker(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new ParallelWorker(cancelPendingJobsIfAnyOtherFails);
    }

    public IParallelWorker GetParallelWorker()
    {
        return new ParallelWorker();
    }

    public ISemaphoreWorker GetSemaphoreWorker()
    {
        return new SemaphoreWorker();
    }

    public ISemaphoreWorker GetSemaphoreWorker(int maxThreads, bool cancelPendingJobsIfAnyOtherFails)
    {
        if (maxThreads <= 0)
            throw new ArgumentException($"Param \"{nameof(maxThreads)}\" must be greater than zero.");

        return new SemaphoreWorker(maxThreads, cancelPendingJobsIfAnyOtherFails);
    }

    public ISemaphoreWorker GetSemaphoreWorker(int maxThreads)
    {
        if (maxThreads <= 0)
            throw new ArgumentException($"Param \"{nameof(maxThreads)}\" must be greater than zero.");

        return new SemaphoreWorker(maxThreads);
    }

    public ISemaphoreWorker GetSemaphoreWorker(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new SemaphoreWorker(cancelPendingJobsIfAnyOtherFails);
    }
}