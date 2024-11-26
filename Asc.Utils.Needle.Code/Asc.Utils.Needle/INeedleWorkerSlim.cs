namespace Asc.Utils.Needle
{
    /// <summary>
    /// The simplest and most perfomance focused tool to running jobs using multithreading.
    /// All properties and methods of this interface, in all its implementations, are threadsafe
    /// with the exception of Dispose method.
    /// This interface has more than once implementation due to can be a semaphore or a parallel worker.
    /// You should use Needle singleton instance to choose what you need.
    /// Remember I am IDisposable. Please, use me inside a using statement or invoke Dispose method when necessary. 
    /// Maybe by checking this interface you think you need more features. In that case check <see cref="INeedleWorker"/>
    /// </summary>
    public interface INeedleWorkerSlim : IDisposable
    {
        /// <summary>
        /// Raised when worker execution has been canceled, either by a cancellation request or in the case that
        /// CancelPendingJobsIfAnyOtherFails is true and some job fails.
        /// </summary>
        event EventHandler Canceled;

        /// <summary>
        /// You can propagate this token in your jobs so that they can be canceled, either when a cancellation is requested
        /// or when CancelPendingJobsIfAnyOtherFails is true and some job throws an exception.
        /// </summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// If true and a job fails, pending jobs will not be executed.
        /// Running jobs can check CancellationToken.IsCancellationRequested to be canceled as well.
        /// It is true by default but you can change that, if your jobs do not have dependencies on each other.
        /// </summary>
        bool CancelPendingJobsIfAnyOtherFails { get; }

        /// <summary>
        /// Request cancellation to cancel pending jobs that are not currently in progress.
        /// So do not assume that when this method finishes executing the process will have been canceled.
        /// For that purpose subscribe Cancelled event.
        /// </summary>
        /// <exception cref="InvalidOperationException">If worker is not running or if a cancellation has been requested</exception>
        void Cancel();

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
        /// Once all jobs have been completed, can throw AggregateException if any job have failed.
        /// Also at that time you can dispose this instance (by the end of using statement or manually invoking Dispose method)
        /// or you can add more jobs to invoke this method again to run those new jobs.
        /// </summary>
        /// <exception cref="InvalidOperationException">If there is not any job to run or if it is already running.</exception>
        /// <exception cref="AggregateException">If any job fails</exception>
        Task RunAsync();
    }
}
