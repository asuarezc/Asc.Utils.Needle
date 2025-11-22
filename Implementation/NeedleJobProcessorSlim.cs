using System.Threading.Channels;

namespace Asc.Utils.Needle.Implementation;

internal class NeedleJobProcessorSlim : INeedleJobProcessorSlim
{
    private readonly Channel<Func<Task>> _channel;
    private readonly Task[] _workers;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    // Async-compatible pause event to avoid blocking thread-pool threads
    private readonly AsyncManualResetEvent _pauseEvent = new(initialState: false);
    private volatile bool _isStarted = false;
    private volatile bool _isPaused = false;
    private bool _disposedValue;

    public int ThreadPoolSize { get; }
    public OnJobFailedBehaviour OnJobFailedBehaviour { get; }
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;
    public event EventHandler<Exception>? JobFaulted;

    public NeedleJobProcessorSlim(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour)
    {
        ThreadPoolSize = threadPoolSize;
        OnJobFailedBehaviour = onJobFailedBehaviour;

        _channel = Channel.CreateUnbounded<Func<Task>>(new()
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });

        // reserve worker slots but do NOT start tasks until Start() is called
        _workers = new Task[ThreadPoolSize];
    }

    public void ProcessJob(Action job)
    {
        ThrowIfDisposed();

        _channel.Writer.TryWrite(() =>
        {
            job();
            return Task.CompletedTask;
        });
    }

    public void ProcessJob(Func<Task> job)
    {
        ThrowIfDisposed();

        _channel.Writer.TryWrite(job);
    }

    public void Start()
    {
        ThrowIfDisposed();
        ThrowIfAlreadyStarted();
        StartInternal();
    }

    public void Pause()
    {
        ThrowIfDisposed();
        ThrowIfNotStarted();
        ThrowIfPaused();
        PauseInternal();
    }

    public void Resume()
    {
        ThrowIfDisposed();
        ThrowIfNotStarted();
        ThrowIfNotPaused();
        ResumeInternal();
    }

    private void ThrowIfAlreadyStarted()
    {
        if (_isStarted)
            throw new InvalidOperationException("Job processor has already been started.");
    }

    private void ThrowIfNotStarted()
    {
        if (!_isStarted)
            throw new InvalidOperationException("Job processor has not been started yet.");
    }

    private void ThrowIfPaused()
    {
        if (_isPaused)
            throw new InvalidOperationException("Job processor is currently paused.");
    }

    private void ThrowIfNotPaused()
    {
        if (!_isPaused)
            throw new InvalidOperationException("Job processor is not paused.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, nameof(NeedleJobProcessorSlim));
    }

    private void ClearChannel()
    {
        while (_channel.Reader.TryRead(out _)) { }
    }

    private void StartInternal()
    {
        _isStarted = true;
        _isPaused = false;
        _pauseEvent.Set(); // allow workers to proceed

        // start worker tasks now (deferred start)
        for (int i = 0; i < ThreadPoolSize; i++)
        {
            // only start if not already started (defensive)
            if (_workers[i] == null)
                _workers[i] = Task.Run(() => WorkerLoop(_cancellationTokenSource.Token));
        }
    }

    private void PauseInternal()
    {
        _isPaused = true;
        _pauseEvent.Reset(); // pause workers asynchronously
    }

    private void ResumeInternal()
    {
        _isPaused = false;
        _pauseEvent.Set(); // resume workers
    }

    private void StopInternal()
    {
        PauseInternal();
        ClearChannel();
        _cancellationTokenSource.Cancel();
    }

    private async Task WorkerLoop(CancellationToken stopToken)
    {
        while (!stopToken.IsCancellationRequested)
        {
            // await asynchronously until started/resumed (does not block threadpool thread)
            await _pauseEvent.WaitAsync(stopToken).ConfigureAwait(false);

            if (stopToken.IsCancellationRequested)
                break;

            try
            {
                // ReadAsync waits until an item is available or cancelled.
                var job = await _channel.Reader.ReadAsync(stopToken).ConfigureAwait(false);

                // If paused right after ReadAsync, still execute the job (consistent with previous behavior).
                try
                {
                    await job().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    JobFaulted?.Invoke(this, ex);

                    if (OnJobFailedBehaviour == OnJobFailedBehaviour.CancelPendingJobs)
                        ClearChannel();
                }
            }
            catch (OperationCanceledException)
            {
                // cancellation requested: exit loop
                break;
            }
            catch (ChannelClosedException)
            {
                // channel closed: exit
                break;
            }
            // loop continues to await pause/resume and read next job
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;

        if (disposing)
        {
            StopInternal();

            try
            {
                // wait only started worker tasks
                var started = _workers.Where(t => t != null).ToArray()!;
                if (started.Length > 0)
                    Task.WhenAll(started).Wait();
            }
            catch { } // ignore exceptions during disposal

            _pauseEvent.Dispose();
            _cancellationTokenSource.Dispose();
        }

        _disposedValue = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #region AsyncManualResetEvent (small, efficient)
    private sealed class AsyncManualResetEvent : IDisposable
    {
        private volatile TaskCompletionSource<bool>? _tcs;

        public AsyncManualResetEvent(bool initialState)
        {
            _tcs = initialState ? new(TaskCreationOptions.RunContinuationsAsynchronously) { } : new();
            if (initialState)
                _tcs.SetResult(true);
            else
                _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            var tcs = _tcs!;
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            // Fast path: already set
            if (tcs.Task.IsCompleted)
                return Task.CompletedTask;

            if (cancellationToken.CanBeCanceled)
                return tcs.Task.WaitAsync(cancellationToken);

            return tcs.Task;
        }

        public void Set()
        {
            var tcs = _tcs!;
            // switch to a completed TCS
            var prev = Interlocked.Exchange(ref _tcs, TaskCompletionSourceFromResult());
            prev?.TrySetResult(true);
        }

        public void Reset()
        {
            // replace with a new non-completed TCS
            Interlocked.Exchange(ref _tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        private static TaskCompletionSource<bool> TaskCompletionSourceFromResult()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(true);
            return tcs;
        }

        public void Dispose()
        {
            // no unmanaged resources
        }
    }
    #endregion
}