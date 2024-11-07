using Asc.Utils.Needle.Implementation;

namespace Asc.Utils.Needle;

/// <summary>
/// The simplest IPincushion singleton implementation for the simplest possible needle factory
/// </summary>
public sealed class Pincushion : IPincushion
{
    private static readonly Lazy<IPincushion> lazyInstance = new(() => new Pincushion(), LazyThreadSafetyMode.PublicationOnly);

    private Pincushion() { }

    public static IPincushion Instance => lazyInstance.Value;

    public IMasterNeedleWorker MasterNeedle => MasterNeedleWorker.Instance;

    public INeedleWorker GetNeedle(int maxThreads, bool cancelPendingJobsIfAnyOtherFails)
    {
        if (maxThreads <= 0)
            throw new ArgumentException($"Param \"{nameof(maxThreads)}\" must be greater than zero.");

        return new NeedleWorker(maxThreads, cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorker GetNeedle(int maxThreads)
    {
        if (maxThreads <= 0)
            throw new ArgumentException($"Param \"{nameof(maxThreads)}\" must be greater than zero.");

        return new NeedleWorker(maxThreads);
    }

    public INeedleWorker GetNeedle(bool cancelPendingJobsIfAnyOtherFails)
    {
        return new NeedleWorker(cancelPendingJobsIfAnyOtherFails);
    }

    public INeedleWorker GetNeedle()
    {
        return new NeedleWorker();
    }
}