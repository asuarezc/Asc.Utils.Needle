namespace Asc.Utils.Needle;

/// <summary>
/// Specifies the behavior to apply when a job fails within a batch processing operation
/// in a <see cref="INeedleWorkerSlim"/> or a <see cref="INeedleWorker"/>.
/// </summary>
/// <remarks>
/// Use this enumeration to control whether pending jobs are canceled or allowed to continue running
/// after a job failure occurs. The selected value determines how the worker responds to failures and can affect the
/// overall progress and completion of batch operations.
/// </remarks>
public enum OnJobFailedBehaviour
{
    CancelPendingJobs,
    ContinueRunningPendingJobs
}