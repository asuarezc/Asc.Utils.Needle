namespace Asc.Utils.Needle;

/// <summary>
/// I am a INeedleWorker and INeedleWorkerSlim factory, so I am a Pincushion. Do  you get the joke?
/// First of all, decide if you need a semaphore or a parallel worker. Decide if you need a slim implementation
/// or a full one, then invoke the corresponding method. Slim implementations are performance focused but you
/// cannot get information about progress or pretty much anything else. Otherwise a full implementation has
/// more properties and events so you can use it, for example, to manage a progress bar.
/// See <see cref="INeedleWorkerSlim"/> and <seealso cref="INeedleWorker"/> to get more information about.
/// </summary>
public interface IPincushion
{
    #region Semaphores

    /// <summary>
    /// Gets a semaphore worker slim implementation with Environment.ProcessorCount available threads.
    /// </summary>
    INeedleWorkerSlim GetSemaphoreWorkerSlim();

    /// <summary>
    /// Gets a semaphore worker slim implementation with a certain amount of available threads.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads);

    /// <summary>
    /// Gets a semaphore worker slim implementation with Environment.ProcessorCount available threads.
    /// If your jobs are codependent, set <paramref name="cancelPendingJobsIfAnyOtherFails"/> to true.
    /// </summary>
    /// <param name="cancelPendingJobsIfAnyOtherFails">
    /// If true and a job fails, pending jobs will not be executed.
    /// In progress jobs will be canceled by checking cancellation token.
    /// </param>
    INeedleWorkerSlim GetSemaphoreWorkerSlim(bool cancelPendingJobsIfAnyOtherFails);

    /// <summary>
    /// Gets a semaphore worker slim implementation with a certain amount of available threads.
    /// If your jobs are codependent, set <paramref name="cancelPendingJobsIfAnyOtherFails"/> to true.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    /// <param name="cancelPendingJobsIfAnyOtherFails">
    /// If true and a job fails, pending jobs will not be executed.
    /// In progress jobs will be canceled by checking cancellation token.
    /// </param>
    INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads, bool cancelPendingJobsIfAnyOtherFails);

    /// <summary>
    /// Gets a semaphore worker implementation with Environment.ProcessorCount available threads.
    /// </summary>
    INeedleWorker GetSemaphoreWorker();

    /// <summary>
    /// Gets a semaphore worker implementation with a certain amount of available threads.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    INeedleWorker GetSemaphoreWorker(int maxThreads);

    /// <summary>
    /// Gets a semaphore worker implementation with Environment.ProcessorCount available threads.
    /// If your jobs are codependent, set <paramref name="cancelPendingJobsIfAnyOtherFails"/> to true.
    /// </summary>
    /// <param name="cancelPendingJobsIfAnyOtherFails">
    /// If true and a job fails, pending jobs will not be executed.
    /// In progress jobs will be canceled by checking cancellation token.
    /// </param>
    INeedleWorker GetSemaphoreWorker(bool cancelPendingJobsIfAnyOtherFails);

    /// <summary>
    /// Gets a semaphore worker implementation with a certain amount of available threads.
    /// If your jobs are codependent, set <paramref name="cancelPendingJobsIfAnyOtherFails"/> to true.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    /// <param name="cancelPendingJobsIfAnyOtherFails">
    /// If true and a job fails, pending jobs will not be executed.
    /// In progress jobs will be canceled by checking cancellation token.
    /// </param>
    INeedleWorker GetSemaphoreWorker(int maxThreads, bool cancelPendingJobsIfAnyOtherFails);

    #endregion

    #region ParallelWorkers

    /// <summary>
    /// Gets a parallel worker slim implementation.
    /// </summary>
    INeedleWorkerSlim GetParallelWorkerSlim();

    /// <summary>
    /// Gets a parallel worker slim implementation.
    /// If your jobs are codependent, set <paramref name="cancelPendingJobsIfAnyOtherFails"/> to true.
    /// </summary>
    /// <param name="cancelPendingJobsIfAnyOtherFails">
    /// If true and a job fails, pending jobs will not be executed.
    /// In progress jobs will be canceled by checking cancellation token.
    /// </param>
    INeedleWorkerSlim GetParallelWorkerSlim(bool cancelPendingJobsIfAnyOtherFails);

    /// <summary>
    /// Gets a parallel worker implementation.
    /// </summary>
    INeedleWorker GetParallelWorker();

    /// <summary>
    /// Gets a parallel worker implementation.
    /// If your jobs are codependent, set <paramref name="cancelPendingJobsIfAnyOtherFails"/> to true.
    /// </summary>
    /// <param name="cancelPendingJobsIfAnyOtherFails">
    /// If true and a job fails, pending jobs will not be executed.
    /// In progress jobs will be canceled by checking cancellation token.
    /// </param>
    INeedleWorker GetParallelWorker(bool cancelPendingJobsIfAnyOtherFails);

    #endregion
}