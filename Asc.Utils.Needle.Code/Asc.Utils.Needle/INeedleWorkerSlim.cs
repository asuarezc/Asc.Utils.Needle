namespace Asc.Utils.Needle;

/// <summary>
/// Defines a contract for a lightweight worker that schedules, manages, and executes jobs with support for cooperative
/// cancellation and job failure handling.
/// </summary>
/// <remarks>
/// Implementations of this interface allow clients to add synchronous or asynchronous jobs, control
/// execution, and observe or request cancellation. The interface provides mechanisms to handle job failures according
/// to a specified behavior and to monitor cancellation requests via events and tokens. It is intended for scenarios
/// where lightweight, programmatic job scheduling and cancellation are required.
/// </remarks>
public interface INeedleWorkerSlim : IDisposable
{
    /// <summary>
    /// Occurs when the operation is canceled.
    /// </summary>
    /// <remarks>
    /// Subscribe to this event to be notified when a cancellation request has been made. Handlers
    /// are invoked on the thread that triggers the cancellation.
    /// </remarks>
    event EventHandler Canceled;

    /// <summary>
    /// Gets the <see cref="CancellationToken"/> used to observe cancellation requests for the current operation.
    /// </summary>
    /// <remarks>
    /// Use this token to monitor for cancellation and respond appropriately in long-running or
    /// asynchronous operations. Observing the token allows cooperative cancellation between the caller and the
    /// operation.
    /// </remarks>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the behavior that determines how the system responds when a job fails.
    /// </summary>
    OnJobFailedBehaviour OnJobFailedBehaviour { get; }

    /// <summary>
    /// Requests cancellation of the current operation, if it is in progress.
    /// </summary>
    /// <remarks>
    /// Calling this method signals that the ongoing operation should be stopped as soon as possible.
    /// The exact timing and effect of cancellation depend on the implementation. After calling this method, the
    /// operation may complete, partially complete, or be rolled back, depending on the context.
    /// </remarks>
    /// <exception>
    /// Throws <see cref="InvalidOperationException"/> if worker is not running or if a previous cancellation request is in progress.
    /// </exception>
    void Cancel();

    /// <summary>
    /// Adds a job to the scheduler for execution.
    /// </summary>
    /// <param name="job">The action to execute as a job. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>
    /// <exception cref="InvalidOperationException">If worker is running.</exception>
    void AddJob(Action job);

    /// <summary>
    /// Adds an asynchronous job to the processing queue.
    /// </summary>
    /// <param name="job">A delegate that represents the asynchronous job to be executed. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>
    /// <exception cref="InvalidOperationException">If worker is running.</exception>
    void AddJob(Func<Task> job);

    /// <summary>
    /// Asynchronously executes the operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    /// <exception>Throws <see cref="InvalidOperationException"/> if no jobs have been added to run.</exception>
    /// <exception>Thorws <see cref="AggregateException"/> if any job fails.</exception>
    Task RunAsync();
}
