namespace Asc.Utils.Needle;

/// <summary>
/// Just ask for needles, I will give you whatever you want.
/// </summary>
public interface IPincushion
{
    /// <summary>
    /// Psss! This method gives you a new INeedleWorker instance. Do not forget to dispose the instance when all your jobs are done.
    /// </summary>
    /// <param name="maxThreads">Maximum threads to run all your jobs</param>
    /// <param name="cancelPendingJobsIfAnyOtherFails">If true and a job fails, any other not running and pending jobs will be canceled.</param>
    /// <returns>A new INeedleWorker instance. This pincushion size is infinite!</returns>
    /// <exception cref="ArgumentException">If <paramref name="maxThreads"/> value is equals or lower than zero.</exception>
    INeedleWorker GetNeedle(int maxThreads, bool cancelPendingJobsIfAnyOtherFails);

    INeedleWorker GetNeedle(int maxThreads);

    INeedleWorker GetNeedle(bool cancelPendingJobsIfAnyOtherFails);

    INeedleWorker GetNeedle();

    /// <summary>
    /// Brings you access to the master needle worker singleton instance.
    /// </summary>
    IMasterNeedleWorker MasterNeedle { get; }
}