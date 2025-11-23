using System.ComponentModel;

namespace Asc.Utils.Needle;

/// <summary>
/// Defines members for tracking the status and progress of jobs processed by the system.
/// </summary>
/// <remarks>Implementations of this interface provide properties to monitor the number of jobs that have
/// completed successfully, faulted, or are currently managed. This interface extends INotifyPropertyChanged to support
/// data binding and change notification scenarios.</remarks>
public interface INeedleJobProcessor : INeedleJobProcessorSlim, INotifyPropertyChanged
{
    /// <summary>
    /// Gets the total number of jobs that have been processed successfully.
    /// </summary>
    int SuccessfullyCompletedJobsCount { get; }

    /// <summary>
    /// Gets the total number of processed jobs that have entered a faulted state.
    /// </summary>
    int FaultedJobsCount { get; }

    /// <summary>
    /// Gets the total number of jobs currently managed by the system.
    /// </summary>
    int TotalJobsCount { get; }
}