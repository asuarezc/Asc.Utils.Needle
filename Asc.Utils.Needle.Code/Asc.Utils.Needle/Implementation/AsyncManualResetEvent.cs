namespace Asc.Utils.Needle.Implementation;

/// <summary>
/// Provides an asynchronous manual-reset event that allows tasks to wait for a signal to be set or reset. This class
/// enables coordination between asynchronous operations by allowing one or more tasks to wait until the event is
/// signaled.
/// </summary>
/// <remarks>
/// Unlike AutoResetEvent, a manual-reset event remains signaled until it is explicitly reset, allowing
/// multiple waiting tasks to proceed. This class is designed for use in asynchronous programming scenarios where
/// traditional synchronization primitives are not suitable. All members are thread-safe.
/// </remarks>
internal sealed class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> _taskCompletionSource;

    public AsyncManualResetEvent(bool initialState)
    {
        _taskCompletionSource = initialState ? new(TaskCreationOptions.RunContinuationsAsynchronously) : new();

        if (initialState)
            _taskCompletionSource.SetResult(true);
        else
            _taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        if (_taskCompletionSource.Task.IsCompleted)
            return Task.CompletedTask;

        if (cancellationToken.CanBeCanceled)
            return _taskCompletionSource.Task.WaitAsync(cancellationToken);

        return _taskCompletionSource.Task;
    }

    public void Set()
    {
        TaskCompletionSource<bool> previous = Interlocked.Exchange(
            ref _taskCompletionSource,
            TaskCompletionSourceFromResult()
        );

        previous?.TrySetResult(true);
    }

    public void Reset()
    {
        Interlocked.Exchange(
            ref _taskCompletionSource,
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        );
    }

    private static TaskCompletionSource<bool> TaskCompletionSourceFromResult()
    {
        var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        taskCompletionSource.SetResult(true);

        return taskCompletionSource;
    }
}