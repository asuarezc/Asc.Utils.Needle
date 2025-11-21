namespace Asc.Utils.Needle;

/// <summary>
/// Specifies the operational status of a <see cref="INeedleJobProcessorSlim"/> or a <see cref="INeedleJobProcessor"/>.
/// </summary>
/// <remarks>
/// Use this enumeration to determine or control the current state of a job processor.
/// The status indicates whether the processor is actively handling jobs, idle and ready, paused, or stopped.
/// This can be useful for monitoring, diagnostics, or managing job processing workflows.
/// </remarks>
public enum NeedleJobProcessorStatus
{
    ProcessingJobs,
    Idle,
    Paused,
    Stopped
}
