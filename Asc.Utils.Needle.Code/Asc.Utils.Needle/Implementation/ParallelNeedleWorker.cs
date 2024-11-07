using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asc.Utils.Needle.Implementation
{
    internal class ParallelNeedleWorker : IParallelNeedleWorker
    {
        #region IParallelNeedleWorker implementation

        public CancellationToken CancellationToken => throw new NotImplementedException();

        public int MaxThreads => throw new NotImplementedException();

        public int TotalJobsCount => throw new NotImplementedException();

        public int CompletedJobsCount => throw new NotImplementedException();

        public int Progress => throw new NotImplementedException();

        public bool CancelPendingJobsIfAnyOtherFails => throw new NotImplementedException();

        public bool IsRunning => throw new NotImplementedException();

        public event EventHandler? Completed;
        public event EventHandler<Exception>? JobFaulted;
        public event EventHandler? Canceled;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void AddJob(Action job)
        {
            throw new NotImplementedException();
        }

        public void AddJob(Func<Task> job)
        {
            throw new NotImplementedException();
        }

        public void BeginRun()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void RequestCancellation()
        {
            throw new NotImplementedException();
        }

        public Task RunAsync()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
