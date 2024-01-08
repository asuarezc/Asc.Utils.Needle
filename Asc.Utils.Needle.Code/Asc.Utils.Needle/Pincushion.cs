using System;

namespace Asc.Utils.Needle;

/// <summary>
/// The simplest IPincushion singleton implementation for the simplest possible needle factory
/// </summary>
public sealed class Pincushion : IPincushion
{
    private static readonly Lazy<IPincushion> lazyInstance = new(() => new Pincushion(), LazyThreadSafetyMode.PublicationOnly);

    private Pincushion() { }

    public static IPincushion Instance => lazyInstance.Value;

    public INeddleWorker GetNeedle(int maxThreads = 3, bool cancelPendingJobsIfAnyOtherFails = true)
    {
        if (maxThreads <= 0)
            throw new ArgumentException($"Param \"{nameof(maxThreads)}\" must be greater than zero.");

        return new Implementation.NeddleWorker(maxThreads, cancelPendingJobsIfAnyOtherFails);
    }
}