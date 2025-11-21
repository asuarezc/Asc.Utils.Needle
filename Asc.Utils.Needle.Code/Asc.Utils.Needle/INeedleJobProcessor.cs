using System.ComponentModel;

namespace Asc.Utils.Needle;

/// <summary>
/// Defines the contract for a job processor that supports job execution and notifies clients of property changes.
/// </summary>
/// <remarks>
/// Implementations of this interface provide mechanisms for processing jobs and support property change
/// notifications via the INotifyPropertyChanged interface. This allows consumers to observe changes to relevant
/// properties, such as processor status, current running jobs count, or total completed jobs count, in real time.
/// </remarks>
public interface INeedleJobProcessor : INeedleJobProcessorSlim, INotifyPropertyChanged { }
