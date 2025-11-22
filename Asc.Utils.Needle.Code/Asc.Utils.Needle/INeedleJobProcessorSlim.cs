namespace Asc.Utils.Needle;

/// <summary>
/// Defines the contract for a lightweight job processor that manages the execution of jobs using a thread pool, with
/// support for job failure handling and processor state management.
/// </summary>
/// <remarks>
/// The implementation of this interface allow scheduling and processing of jobs, either as synchronous
/// actions or asynchronous tasks, with configurable thread pool size and job failure behavior. The processor exposes
/// events for job fault notifications and provides methods to control its execution state, including starting, pausing,
/// and resuming job processing. Resources used by the processor should be released by calling the appropriate dispose
/// methods when the processor is no longer needed.
/// </remarks>
public interface INeedleJobProcessorSlim : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the maximum number of threads available in the thread pool for concurrent task execution.
    /// </summary>
    int ThreadPoolSize { get; }

    /// <summary>
    /// Gets the behavior to apply when a job fails during execution.
    /// </summary>
    OnJobFailedBehaviour OnJobFailedBehaviour { get; }

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> that is used to observe cancellation requests for the current
    /// operation.
    /// </summary>
    /// <remarks>
    /// Use this token to monitor for cancellation and respond appropriately in long-running or
    /// asynchronous operations. The token may be used to cooperatively cancel the operation if requested by the
    /// caller.
    /// </remarks>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the current status of the job processor, indicating whether it is running, paused, or stopped.
    /// </summary>
    NeedleJobProcessorStatus Status { get; }

    /// <summary>
    /// Occurs when a job encounters an unhandled exception during execution.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to handle errors that occur within a job. The event provides
    /// the exception that caused the fault, allowing for custom error handling or logging. This event is raised only
    /// for unhandled exceptions; handled exceptions within the job do not trigger this event.
    /// </remarks>
    event EventHandler<Exception> JobFaulted;

    /// <summary>
    /// Adds a job to the processing queue for execution.
    /// </summary>
    /// <param name="job">The action to execute. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="job"/> is null.</exception>
    void ProcessJob(Action job);

    /// <summary>
    /// Adds an asynchronous job to the processing queue for execution.
    /// </summary>
    /// <param name="job">The asyncronous action to execute. Cannot be null</param>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="job"/> is null.</exception>
    void ProcessJob(Func<Task> job);

    /// <summary>
    /// Starts the job processor, allowing it to begin processing queued jobs.
    /// </summary>
    /// <remarks>
    /// Jobs can be added to the processor before it is started, but they will not be executed until
    /// it is started. Calling this method on an already started processor will throw an <see cref="InvalidOperationException"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if the processor has already been started.</exception>
    void Start();

    /// <summary>
    /// Pauses the job processor, temporarily halting the processing of queued jobs.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the processor is not currently running or if the processor is already paused.</exception>
    void Pause();

    /// <summary>
    /// Resumes the job processor, allowing it to continue processing queued jobs after being paused.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the processor is not currently paused.</exception>
    void Resume();
}
