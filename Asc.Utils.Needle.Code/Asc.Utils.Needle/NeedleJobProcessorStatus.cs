namespace Asc.Utils.Needle;

/// <summary>
/// Specifies the operational status of a Needle job processor.
/// </summary>
/// <remarks>
/// Use this enumeration to determine or control whether the job processor is actively processing jobs,
/// temporarily paused, or has been stopped. The status can be used to manage workflow execution and respond
/// appropriately to changes in processor state.
/// </remarks>
public enum NeedleJobProcessorStatus
{
    Running,
    Paused,
    Stopped
}