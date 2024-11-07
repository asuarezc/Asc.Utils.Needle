using System.ComponentModel;

namespace Asc.Utils.Needle;

/// <summary>
/// The siempliest multithreading tool. Use 3 threads to run every job you need.
/// Works like INeddleWorker but you cannot dispose this instance.
/// Jobs are executed when they are added, if there are available threads.
/// Otherwise they remain in a queue waiting for an available thread.
/// So it has a FIFO behavior. It is threadsafe.
/// </summary>
public interface IMasterNeddleWorker
{
    /// <summary>
    /// Adds a synchronous job.
    /// </summary>
    /// <param name="job">Job to run</param>
    /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>
    Task AddJobAsync(Action job);

    /// <summary>
    /// Adds an asynchronous job.
    /// </summary>
    /// <param name="job">Job to run</param>
    /// <exception cref="ArgumentNullException">If <paramref name="job"/> is null.</exception>
    Task AddJobAsync(Func<Task> job);

    /// <summary>
    /// Number of current jobs in stack, running or waiting.
    /// </summary>
    int CurrentJobsStackSize { get; }

    /// <summary>
    /// Tells you if master neddle worker is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised when a job is successfully completed.
    /// </summary>
    event EventHandler? JobCompleted;

    /// <summary>
    /// Raised when a job throws an exception.
    /// </summary>
    event EventHandler<Exception>? JobFaulted;
}
