using System.ComponentModel;

namespace Asc.Utils.Needle;

/// <summary>
/// Defines an interface for a worker that manages and monitors the execution of jobs, providing status information and
/// fault notification.
/// </summary>
/// <remarks>Implementations of this interface support job tracking, status reporting, and error notification
/// through events. The interface extends <see cref="INeedleWorkerSlim"/> for basic worker functionality and <see
/// cref="INotifyPropertyChanged"/> to support property change notifications, enabling integration with data binding or
/// UI frameworks.</remarks>
public interface INeedleWorker : INeedleWorkerSlim, INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a job encounters an unhandled exception during execution.
    /// </summary>
    /// <remarks>
    /// Subscribers can use this event to handle errors that occur within a job. The event provides
    /// the exception that caused the fault, allowing for custom error handling or logging. This event is typically
    /// raised when a job cannot complete successfully due to an unexpected error.
    /// </remarks>
    event EventHandler<Exception> JobFaulted;

    /// <summary>
    /// Gets a value indicating whether the process or operation is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the total number of jobs currently managed by the system.
    /// </summary>
    int TotalJobsCount { get; }

    /// <summary>
    /// Gets the number of jobs that have completed successfully.
    /// </summary>
    int SuccessfullyCompletedJobsCount { get; }

    /// <summary>
    /// Gets the number of jobs that have entered a faulted state.
    /// </summary>
    int FaultedJobsCount { get; }
}