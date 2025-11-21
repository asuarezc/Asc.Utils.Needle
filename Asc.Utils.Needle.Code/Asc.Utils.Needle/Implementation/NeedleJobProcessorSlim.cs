using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asc.Utils.Needle.Implementation;

internal class NeedleJobProcessorSlim : INeedleJobProcessorSlim
{
    public int ThreadPoolSize => throw new NotImplementedException();

    public int CurrentRunningJobsCount => throw new NotImplementedException();

    public int TotalCompletedJobsCount => throw new NotImplementedException();

    public NeedleJobProcessorStatus Status => throw new NotImplementedException();

    public OnJobFailedBehaviour OnJobFailedBehaviour => throw new NotImplementedException();

    public CancellationToken CancellationToken => throw new NotImplementedException();

    public event EventHandler<Exception>? JobFaulted;

    public void Pause()
    {
        throw new NotImplementedException();
    }

    public void ProcessJob(Action job)
    {
        throw new NotImplementedException();
    }

    public void ProcessJob(Func<Task> job)
    {
        throw new NotImplementedException();
    }

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }
}
