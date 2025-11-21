namespace Asc.Utils.Needle;

/// <summary>
/// Defines a minimal interface for processing jobs using a thread pool with support for synchronous and asynchronous
/// job execution, status monitoring, and basic lifecycle control.
/// </summary>
/// <remarks>Implementations of this interface allow jobs to be queued for execution and provide properties to
/// monitor processing status and job counts. The interface exposes events for job fault handling and methods to start,
/// pause, or stop job processing.</remarks>
public interface INeedleJobProcessorSlim
{
    /// <summary>
    /// Gets the maximum number of threads used by this job processor.
    /// </summary>
    int ThreadPoolSize { get; }

    /// <summary>
    /// Gets the number of jobs that are currently running.
    /// </summary>
    int CurrentRunningJobsCount { get; }

    /// <summary>
    /// Gets the total number of jobs that have completed successfully or unsuccessfully.
    /// </summary>
    int TotalCompletedJobsCount { get; }

    /// <summary>
    /// Gets the current status of the job processor.
    /// </summary>
    NeedleJobProcessorStatus Status { get; }

    /// <summary>
    /// Gets the behavior to apply when a job fails during execution.
    /// </summary>
    OnJobFailedBehaviour OnJobFailedBehaviour { get; }

    /// <summary>
    /// Gets the token that can be used to observe cancellation by added jobs.
    /// <see cref="CancellationToken.IsCancellationRequested"/> will be true when any job fails and when <see cref="OnJobFailedBehaviour"/> is set to <see cref="OnJobFailedBehaviour.CancelPendingJobs"/>.
    /// Also, it will be true when <see cref="Pause"/> or <see cref="Stop"/> methods are invoked.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Occurs when a job encounters an unhandled exception during execution.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to handle errors that occur within a job and perform custom
    /// error handling or logging. The event provides the exception that caused the job to fault.
    /// </remarks>
    event EventHandler<Exception> JobFaulted;

    /// <summary>
    /// Adds a job to the processing queue to be executed when a thread is available.
    /// </summary>
    /// <param name="job">Job to process</param>
    /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>"
    void ProcessJob(Action job);

    /// <summary>
    /// Adds an asyncronous job to the processing queue to be executed when a thread is available.
    /// </summary>
    /// <param name="job">Asyncronous job to process</param>
    /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>"
    void ProcessJob(Func<Task> job);

    /// <summary>
    /// Starts processing jobs in the queue.
    /// </summary>
    void Start();

    /// <summary>
    /// Pauses the job processor but does not remove jobs from the queue.
    /// When resumed, processing continues with the remaining jobs.
    /// </summary>
    void Pause();

    /// <summary>
    /// Stops processing jobs. Any jobs that are currently running will be allowed to complete,
    /// but no new jobs will be started, and pending jobs in the queue will be discarded.
    /// </summary>
    void Stop();
}
