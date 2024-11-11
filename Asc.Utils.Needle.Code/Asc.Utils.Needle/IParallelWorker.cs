using System.ComponentModel;

namespace Asc.Utils.Needle
{
    /// <summary>
    /// Works like INeedleWorker but starting all jobs at same time. One thread per job.
    /// </summary>
    public interface IParallelWorker : INotifyPropertyChanged, IDisposable
    {
        /// <summary>
        /// Raised when all jobs have been completed (successfully or not). Remember to subscribe to this event only
        /// if you haved decided to invoke BeginRun since RunAsync can be awaited
        /// </summary>
        event EventHandler Completed;

        /// <summary>
        /// Raised when a job fails. Remember to subscribe to this event only if you haved decided to invoke BeginRun
        /// since RunAsync can be awaited and can throws AggregateException, which includes a collection of caught exceptions
        /// </summary>
        event EventHandler<Exception> JobFaulted;

        /// <summary>
        /// Raised when Run operation has been canceled
        /// </summary>
        event EventHandler Canceled;

        /// <summary>
        /// You can use this inside your jobs to cancel while doing a certain job instead of wait to complete a previous one
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Number of total added jobs
        /// </summary>
        int TotalJobsCount { get; }

        /// <summary>
        /// Number of completed jobs
        /// </summary>
        int CompletedJobsCount { get; }

        /// <summary>
        /// Progress percentage
        /// </summary>
        int Progress { get; }

        /// <summary>
        /// If true and a job fails, pending and not in progress jobs will be cancelled.
        /// </summary>
        bool CancelPendingJobsIfAnyOtherFails { get; }

        /// <summary>
        /// Returns true if worker is running, otherwise returns false.
        /// You can add jobs if "IsRunning" is false.
        /// You can cancel pending jobs that are not currently in progress if "IsRunning" is true.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Adds a synchronous job.
        /// </summary>
        /// <param name="job">Job to run</param>
        /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>
        /// <exception cref="InvalidOperationException">If worker is running.</exception>
        void AddJob(Action job);

        /// <summary>
        /// Adds an asynchronous job.
        /// </summary>
        /// <param name="job">Job to run</param>
        /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>
        /// <exception cref="InvalidOperationException">If worker is running.</exception>
        void AddJob(Func<Task> job);

        /// <summary>
        /// Remember not to invoke this method inside an using statement as the worker
        /// may have been disposed before all jobs have been executed.
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is not any job to run.</exception>
        void BeginRun();

        /// <summary>
        /// Request cancellation to cancel pending jobs that are not currently in progress.
        /// Use this method with BeginRun only since RunAsync can be awaited.
        /// </summary>
        /// <exception cref="InvalidOperationException">If worker is not running</exception>
        void RequestCancellation();

        /// <summary>
        /// Once all jobs have been completed, can throw AggregateException if any job have failed.
        /// Also at that time you can dispose this instance (by the end of using statement or manually invoking Dispose method)
        /// or you can add more jobs to invoke this method again to run those new jobs.
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is not any job to run.</exception>
        /// <exception cref="AggregateException">If any job fails</exception>
        Task RunAsync();
    }
}
