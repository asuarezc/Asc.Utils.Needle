using System;

namespace Asc.Utils.Needle;

/// <summary>
/// Just ask for needles, I will give you whatever you want
/// </summary>
public interface IPincushion
{
    /// <summary>
    /// Psss! This method gives you a new INeddleWorker instance.
    /// </summary>
    /// <param name="maxThreads">Maximum threads to run all your jobs</param>
    /// <param name="cancelPendingJobsIfAnyOtherFails">If true and a job fails, any other not running and pending jobs will be canceled.</param>
    /// <returns>A new INeddleWorker instance. This pincushion size is infinite!</returns>
    /// <exception cref="ArgumentException">If <paramref name="maxThreads"/> value is equals or lower than zero.</exception>
    INeddleWorker GetNeedle(int maxThreads = 3, bool cancelPendingJobsIfAnyOtherFails = true);
}