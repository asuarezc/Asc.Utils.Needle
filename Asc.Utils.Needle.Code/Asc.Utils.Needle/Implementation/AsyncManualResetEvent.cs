namespace Asc.Utils.Needle.Implementation;

internal sealed class AsyncManualResetEvent : IAsyncManualResetEvent
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