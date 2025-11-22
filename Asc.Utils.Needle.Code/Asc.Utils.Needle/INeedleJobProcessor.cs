using System.ComponentModel;

namespace Asc.Utils.Needle;

public interface INeedleJobProcessor : INeedleJobProcessorSlim, INotifyPropertyChanged
{
    int TotalSuccessfullyProcessedJobsCount { get; }

    int TotalFaultedProcessedJobsCount { get; }

    int TotalAddedJobsCount { get; }
}