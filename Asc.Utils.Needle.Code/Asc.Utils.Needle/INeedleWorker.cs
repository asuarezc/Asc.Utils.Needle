﻿using System.ComponentModel;

namespace Asc.Utils.Needle;

/// <summary>
/// I can do every thing that <see cref="INeedleWorkerSlim"/> does but I am more focused on feedback.
/// So this is your choice for frontend applications since you can give feedback to users.
/// All properties and methods of this interface, in all its implementations, are threadsafe
/// with the exception of Dispose method.
/// This interface has more than once implementation due to can be a semaphore or a parallel worker.
/// You should use Needle singleton instance to choose what you need.
/// Remember I am IDisposable. Please, use me inside a using statement or invoke Dispose method when necessary.
/// </summary>
public interface INeedleWorker : INeedleWorkerSlim, INotifyPropertyChanged
{
    /// <summary>
    /// Raised when a job fails so you can check exception before this worker RunAsync method throws an AggregateException
    /// with the same exception instance inside its inners exceptions property.
    /// Usefull also if you haved decided to invoke BeginRun instead awaiting RunAsync.
    /// </summary>
    event EventHandler<Exception> JobFaulted;

    /// <summary>
    /// Returns true if worker is running, otherwise returns false.
    /// You can subscribe to the PropertyChanged event to check if this property has changed its value.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Number of total added jobs: completed, running, pending, canceled or faulted.
    /// You can subscribe to the PropertyChanged event to check if this property has changed its value.
    /// </summary>
    int TotalJobsCount { get; }

    /// <summary>
    /// Number of successfully completed jobs.
    /// You can subscribe to the PropertyChanged event to check if this property has changed its value.
    /// </summary>
    int SuccessfullyCompletedJobsCount { get; }

    /// <summary>
    /// Number of faulted jobs.
    /// You can subscribe to the PropertyChanged event to check if this property has changed its value.
    /// </summary>
    int FaultedJobsCount { get; }
}