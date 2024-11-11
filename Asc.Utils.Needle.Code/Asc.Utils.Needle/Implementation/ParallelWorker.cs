using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Implementation
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    internal class ParallelWorker(bool cancelPendingJobsIfAnyOtherFails = true) : IParallelWorker
    {
        private bool disposedValue;
        private bool isRunning;
        private int totalJobsCount;
        private int completedJobsCount;

        private static readonly object lockObject = new();
        private CancellationTokenSource cancellationTokenSource = new();

        private readonly List<Action> actionJobs = [];
        private readonly List<Func<Task>> taskJobs = [];
        private readonly List<Exception> exceptions = [];
        private readonly List<Task> tasks = [];

        #region IParallelNeedleWorker implementation

        public CancellationToken CancellationToken
        {
            get
            {
                ThrowIfDisposed();
                return cancellationTokenSource.Token;
            }
        }

        public int TotalJobsCount
        {
            get
            {
                ThrowIfDisposed();
                return totalJobsCount;
            }
            private set
            {
                ThrowIfDisposed();
                totalJobsCount = value;
                NotifyPropertyChanged(nameof(TotalJobsCount));
                NotifyPropertyChanged(nameof(Progress));
            }
        }

        public int CompletedJobsCount
        {
            get
            {
                ThrowIfDisposed();
                return completedJobsCount;
            }
            private set
            {
                ThrowIfDisposed();
                completedJobsCount = value;
                NotifyPropertyChanged(nameof(CompletedJobsCount));
                NotifyPropertyChanged(nameof(Progress));
            }
        }

        public int Progress
        {
            get
            {
                ThrowIfDisposed();
                return TotalJobsCount > 0
                    ? completedJobsCount * 100 / TotalJobsCount
                    : 0;
            }
        }

        public bool CancelPendingJobsIfAnyOtherFails
        {
            get
            {
                ThrowIfDisposed();
                return cancelPendingJobsIfAnyOtherFails;
            }
        }

        public bool IsRunning
        {
            get
            {
                ThrowIfDisposed();
                return isRunning;
            }
            private set
            {
                ThrowIfDisposed();
                isRunning = value;
                NotifyPropertyChanged(nameof(IsRunning));
            }
        }

        public event EventHandler? Completed;
        public event EventHandler<Exception>? JobFaulted;
        public event EventHandler? Canceled;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void AddJob(Action job)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(job);

            if (IsRunning)
                throw new InvalidOperationException("Cannot add jobs while running");

            actionJobs.Add(job);
            TotalJobsCount++;
        }

        public void AddJob(Func<Task> job)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(job);

            if (IsRunning)
                throw new InvalidOperationException("Cannot add jobs while running");

            taskJobs.Add(job);
            TotalJobsCount++;
        }

        public void BeginRun()
        {
            ThrowIfDisposed();

            if (actionJobs.Count == 0 && taskJobs.Count == 0)
                throw new InvalidOperationException("Nothing to run");

            Task.Run(RunAsync);
        }

        public void RequestCancellation()
        {
            ThrowIfDisposed();

            if (!IsRunning)
                throw new InvalidOperationException("ParallelWorker is not running. Operation cannot be canceled.");

            cancellationTokenSource.Cancel();
        }

        public async Task RunAsync()
        {
            ThrowIfDisposed();

            if (actionJobs.Count == 0 && taskJobs.Count == 0)
                throw new InvalidOperationException("Nothing to run");

            IsRunning = true;

            try
            {
                IEnumerable<Task> tasks = Enumerable.Concat(
                    actionJobs.Select(GetTaskFromJob),
                    taskJobs.Select(GetTaskFromFunc)
                );

                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                IsRunning = false;
                ClearWorkCollections();
                cancellationTokenSource = new();
                NotifyPropertyChanged(nameof(CancellationToken));
                Completed?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        private Task GetTaskFromJob(Action job)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        Canceled?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    job();

                    lock (lockObject)
                        CompletedJobsCount++;
                }
                catch (Exception ex)
                {
                    ManageException(ex);
                }
                job();
            });
        }

        private Task GetTaskFromFunc(Func<Task> job)
        {
            return Task.Run(async () =>
            {
                try
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        Canceled?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    await job();

                    lock (lockObject)
                        CompletedJobsCount++;
                }
                catch (Exception ex)
                {
                    ManageException(ex);
                }
            });
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
        }

        private void ManageException(Exception ex)
        {
            if (!CancellationToken.IsCancellationRequested && CancelPendingJobsIfAnyOtherFails)
                cancellationTokenSource.Cancel();

            lock (lockObject)
            {
                exceptions.Add(ex);
                JobFaulted?.Invoke(this, ex);
            }
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ClearWorkCollections()
        {
            actionJobs.Clear();
            taskJobs.Clear();
            exceptions.Clear();
            tasks.Clear();

            TotalJobsCount = 0;
            CompletedJobsCount = 0;
        }

        private string GetDebuggerDisplay() => ToString();

        public override string ToString()
        {
            ThrowIfDisposed();
            return $"IsRunning = {IsRunning}, Progress = ({Progress}% completed {CompletedJobsCount} of {TotalJobsCount} jobs)";
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
                return;

            if (IsRunning)
                throw new InvalidOperationException("Cannot dispose while worker is running");

            if (disposing)
                cancellationTokenSource.Dispose();

            ClearWorkCollections();
            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
