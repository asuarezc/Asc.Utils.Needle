namespace Asc.Utils.Needle;

/// <summary>
/// Defines a contract for creating and retrieving various types of job workers and processors, including
/// semaphore-based, parallel, and job processors, with configurable concurrency and job failure handling
/// behaviors.
/// </summary>
/// <remarks>
/// The IPincushion interface provides factory methods for obtaining worker and processor instances
/// tailored to different concurrency models and job management strategies. It supports customization of thread pool
/// sizes and specifies how job failures are handled, allowing consumers to select appropriate behaviors for their
/// workload. Implementations are expected to enforce parameter constraints, such as requiring positive thread counts,
/// and to document any exceptions that may be thrown for invalid arguments. This interface is intended for advanced job
/// scheduling and parallel processing scenarios where fine-grained control over execution and failure policies is
/// required.
/// </remarks>
public interface IPincushion
{
    #region Semaphores

    /// <summary>
    /// Gets a semaphore worker slim implementation with Environment.ProcessorCount available threads.
    /// By default, <see cref="INeedleWorkerSlim.OnJobFailedBehaviour"/> value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/>.
    /// </summary>
    INeedleWorkerSlim GetSemaphoreWorkerSlim();

    /// <summary>
    /// Gets a semaphore worker slim implementation with a certain amount of available threads.
    /// By default, <see cref="INeedleWorkerSlim.OnJobFailedBehaviour"/> value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/>.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads);

    /// <summary>
    /// Gets a semaphore worker slim implementation with Environment.ProcessorCount available threads.
    /// If your jobs are not codependent, set <paramref name="onJobFailedBehaviour"/> to <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>.
    /// </summary>
    /// <param name="onJobFailedBehaviour">
    /// If value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/> and a job fails, pending jobs will not be executed,
    /// otherwise, if value is <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>, pending jobs will be executed.
    /// </param>
    INeedleWorkerSlim GetSemaphoreWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Gets a semaphore worker slim implementation with a certain amount of available threads.
    /// If your jobs are not codependent, set <paramref name="onJobFailedBehaviour"/> to <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    /// <param name="onJobFailedBehaviour">
    /// If value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/> and a job fails, pending jobs will not be executed,
    /// otherwise, if value is <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>, pending jobs will be executed.
    /// </param>
    INeedleWorkerSlim GetSemaphoreWorkerSlim(int maxThreads, OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Gets a semaphore worker implementation with Environment.ProcessorCount available threads.
    /// By default, <see cref="INeedleWorkerSlim.OnJobFailedBehaviour"/> value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/>.
    /// </summary>
    INeedleWorker GetSemaphoreWorker();

    /// <summary>
    /// Gets a semaphore worker implementation with a certain amount of available threads.
    /// By default, <see cref="INeedleWorkerSlim.OnJobFailedBehaviour"/> value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/>.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    INeedleWorker GetSemaphoreWorker(int maxThreads);

    /// <summary>
    /// Gets a semaphore worker implementation with Environment.ProcessorCount available threads.
    /// If your jobs are not codependent, set <paramref name="onJobFailedBehaviour"/> to <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>.
    /// </summary>
    /// <param name="onJobFailedBehaviour">
    /// If value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/> and a job fails, pending jobs will not be executed,
    /// otherwise, if value is <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>, pending jobs will be executed.
    /// </param>
    INeedleWorker GetSemaphoreWorker(OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Gets a semaphore worker implementation with a certain amount of available threads.
    /// If your jobs are not codependent, set <paramref name="onJobFailedBehaviour"/> to <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>.
    /// </summary>
    /// <param name="maxThreads">Number of threads to use with semaphore.</param>
    /// <param name="onJobFailedBehaviour">
    /// If value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/> and a job fails, pending jobs will not be executed,
    /// otherwise, if value is <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>, pending jobs will be executed.
    /// </param>
    INeedleWorker GetSemaphoreWorker(int maxThreads, OnJobFailedBehaviour onJobFailedBehaviour);

    #endregion

    #region Parallel workers

    /// <summary>
    /// Gets a parallel worker slim implementation.
    /// </summary>
    INeedleWorkerSlim GetParallelWorkerSlim();

    /// <summary>
    /// Gets a parallel worker slim implementation.
    /// If your jobs are not codependent, set <paramref name="onJobFailedBehaviour"/> to <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>.
    /// </summary>
    /// <param name="onJobFailedBehaviour">
    /// If value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/> and a job fails, pending jobs will not be executed,
    /// otherwise, if value is <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>, pending jobs will be executed.
    /// </param>
    INeedleWorkerSlim GetParallelWorkerSlim(OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Gets a parallel worker implementation.
    /// </summary>
    INeedleWorker GetParallelWorker();

    /// <summary>
    /// Gets a parallel worker implementation.
    /// If your jobs are not codependent, set <paramref name="onJobFailedBehaviour"/> to <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>.
    /// </summary>
    /// <param name="onJobFailedBehaviour">
    /// If value is <see cref="OnJobFailedBehaviour.CancelPendingJobs"/> and a job fails, pending jobs will not be executed,
    /// otherwise, if value is <see cref="OnJobFailedBehaviour.ContinueRunningPendingJobs"/>, pending jobs will be executed.
    /// </param>
    INeedleWorker GetParallelWorker(OnJobFailedBehaviour onJobFailedBehaviour);

    #endregion

    #region Job Processors

    /// <summary>
    /// Gets a lightweight job processor for handling job execution with minimal overhead.
    /// </summary>
    /// <returns>An instance of <see cref="INeedleJobProcessorSlim"/> that can be used to process jobs efficiently.</returns>
    INeedleJobProcessorSlim GetNeedleJobProcessorSlim();

    /// <summary>
    /// Creates and returns a lightweight job processor that uses a thread pool with the specified number of threads.
    /// </summary>
    /// <param name="threadPoolSize">The number of threads to allocate for the job processor's thread pool. Must be greater than zero.</param>
    /// <returns>An instance of a lightweight job processor configured to use the specified thread pool size.</returns>
    INeedleJobProcessorSlim GetNeedleJobProcessorSlim(int threadPoolSize);

    /// <summary>
    /// Creates and returns a lightweight job processor configured with the specified behavior for handling job
    /// failures.
    /// </summary>
    /// <param name="onJobFailedBehaviour">Specifies the action to take when a job fails during processing. Determines how the processor responds to job
    /// failures.</param>
    /// <returns>An instance of a lightweight job processor that applies the specified failure handling behavior.</returns>
    INeedleJobProcessorSlim GetNeedleJobProcessorSlim(OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Creates and returns a lightweight job processor configured with the specified thread pool size and job failure
    /// behavior.
    /// </summary>
    /// <param name="threadPoolSize">The number of threads to allocate for processing jobs. Must be greater than zero.</param>
    /// <param name="onJobFailedBehaviour">Specifies the behavior to apply when a job fails during processing.</param>
    /// <returns>
    /// An instance of <see cref="INeedleJobProcessorSlim"/> configured with the provided thread pool size and job
    /// failure behavior.
    /// </returns>
    /// <exception>Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="threadPoolSize"/> is not greater than zero.</exception>
    INeedleJobProcessorSlim GetNeedleJobProcessorSlim(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Gets an instance of a job processor used to execute and manage jobs.
    /// </summary>
    /// <returns>An object that implements the INeedleJobProcessor interface for processing jobs.</returns>
    INeedleJobProcessor GetNeedleJobProcessor();

    /// <summary>
    /// Creates and returns a job processor configured to use a specified number of threads for concurrent job
    /// execution.
    /// </summary>
    /// <param name="threadPoolSize">The number of threads to allocate for the job processor's thread pool. Must be greater than zero.</param>
    /// <returns>An instance of <see cref="INeedleJobProcessor"/> that processes jobs using the specified thread pool size.</returns>
    /// <exception>Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="threadPoolSize"/> is not greater than zero.</exception>
    INeedleJobProcessor GetNeedleJobProcessor(int threadPoolSize);

    /// <summary>
    /// Retrieves an instance of a job processor configured with the specified behavior for handling job failures.
    /// </summary>
    /// <param name="onJobFailedBehaviour">
    /// Specifies the behavior to apply when a job fails. Determines how the processor responds to job failure scenarios.
    /// </param>
    /// <returns>
    /// An instance of <see cref="INeedleJobProcessor"/> that processes jobs according to the provided failure handling behavior.
    /// </returns>
    INeedleJobProcessor GetNeedleJobProcessor(OnJobFailedBehaviour onJobFailedBehaviour);

    /// <summary>
    /// Creates and returns a job processor configured with the specified thread pool size and job failure behavior.
    /// </summary>
    /// <param name="threadPoolSize">The number of threads to allocate for processing jobs. Must be greater than zero.</param>
    /// <param name="onJobFailedBehaviour">Specifies how the job processor should handle failed jobs.</param>
    /// <returns>
    /// An instance of <see cref="INeedleJobProcessor"/> configured with the provided thread pool size and job failure behavior.
    /// </returns>
    /// <exception>Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="threadPoolSize"/> is not greater than zero.</exception>
    INeedleJobProcessor GetNeedleJobProcessor(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour);

    #endregion
}