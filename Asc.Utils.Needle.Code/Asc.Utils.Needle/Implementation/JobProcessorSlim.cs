using System.Diagnostics;
using System.Threading.Channels;

namespace Asc.Utils.Needle.Implementation;

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
internal class JobProcessorSlim : INeedleJobProcessorSlim
{
    private readonly Channel<Func<Task>> _channel;
    private readonly Task[] _workers;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly IAsyncManualResetEvent _pauseEvent;
    private int _isStartedInt;
    private bool _isPaused = false;
    private bool _disposedValue;
    private int _stoppedInt;

    // Track pending jobs for a useful debugger display
    private int _pendingJobsCount;

    public JobProcessorSlim(int threadPoolSize, OnJobFailedBehaviour onJobFailedBehaviour, IAsyncManualResetEvent pauseEvent)
    {
        ThreadPoolSize = threadPoolSize;
        OnJobFailedBehaviour = onJobFailedBehaviour;
        _pauseEvent = pauseEvent;

        _channel = Channel.CreateUnbounded<Func<Task>>(new()
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });

        //Reserve worker slots but do not start tasks until Start() is called
        _workers = new Task[ThreadPoolSize];
    }

    #region INeedleJobProcessorSlim Implementation

    public int ThreadPoolSize { get; }

    public OnJobFailedBehaviour OnJobFailedBehaviour { get; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public event EventHandler<Exception>? JobFaulted;

    public NeedleJobProcessorStatus Status =>
        Volatile.Read(ref _isStartedInt) == 0
            ? NeedleJobProcessorStatus.Stopped
            : Volatile.Read(ref _isPaused)
                ? NeedleJobProcessorStatus.Paused
                : NeedleJobProcessorStatus.Running;

    public void ProcessJob(Action job)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(job);

        var written = _channel.Writer.TryWrite(() =>
        {
            job();
            return Task.CompletedTask;
        });

        if (written)
            Interlocked.Increment(ref _pendingJobsCount);
    }

    public void ProcessJob(Func<Task> job)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(job);

        var written = _channel.Writer.TryWrite(job);

        if (written)
            Interlocked.Increment(ref _pendingJobsCount);
    }

    public void Start()
    {
        ThrowIfDisposed();

        if (Interlocked.CompareExchange(ref _isStartedInt, 1, 0) != 0)
            throw new InvalidOperationException("Job processor has already been started.");

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

    #endregion

    private void ThrowIfNotStarted()
    {
        if (Volatile.Read(ref _isStartedInt) == 0)
            throw new InvalidOperationException("Job processor has not been started yet.");
    }

    private void ThrowIfPaused()
    {
        if (Volatile.Read(ref _isPaused))
            throw new InvalidOperationException("Job processor is currently paused.");
    }

    private void ThrowIfNotPaused()
    {
        if (!Volatile.Read(ref _isPaused))
            throw new InvalidOperationException("Job processor is not paused.");
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposedValue), nameof(JobProcessorSlim));
    }

    private void ClearChannel()
    {
        while (_channel.Reader.TryRead(out _))
        {
            // decrement pending count for items discarded
            Interlocked.Decrement(ref _pendingJobsCount);
        }
    }

    private void StartInternal()
    {
        Volatile.Write(ref _isStartedInt, 1);
        _pauseEvent.Set(); //Set event to start workers

        StartTasks();
    }

    private void StartTasks()
    {
        for (int i = 0; i < ThreadPoolSize; i++)
        {
            //Only start if not already started
            if (_workers[i] == null)
                _workers[i] = Task.Run(() => WorkerLoop(_cancellationTokenSource.Token));
        }
    }

    private void PauseInternal()
    {
        Volatile.Write(ref _isPaused, true);
        _pauseEvent.Reset(); //Reset event to pause workers
    }

    private void ResumeInternal()
    {
        Volatile.Write(ref _isPaused, false);
        _pauseEvent.Set(); //Set event to resume workers
    }

    private void StopInternal()
    {
        if (Interlocked.Exchange(ref _stoppedInt, 1) != 0)
            return;

        PauseInternal();
        _channel.Writer.TryComplete();
        ClearChannel();
        _cancellationTokenSource.Cancel();
    }

    private async Task WorkerLoop(CancellationToken stopToken)
    {
        while (!stopToken.IsCancellationRequested)
        {
            await _pauseEvent.WaitAsync(stopToken).ConfigureAwait(false);

            if (stopToken.IsCancellationRequested)
                break;

            try
            {
                //Wait until an item is available or cancelled.
                Func<Task> job = await _channel.Reader.ReadAsync(stopToken).ConfigureAwait(false);

                // adjust pending counter: we removed one item from the queue
                Interlocked.Decrement(ref _pendingJobsCount);

                try
                {
                    await job().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    try { JobFaulted?.Invoke(this, ex); }
                    catch { } //Ignore exceptions thrown by event handlers

                    if (OnJobFailedBehaviour == OnJobFailedBehaviour.CancelPendingJobs)
                        ClearChannel();
                }
            }
            catch (OperationCanceledException) //Cancellation requested: exit loop
            {
                break;
            }
            catch (ChannelClosedException)
            {
                break;
            }
        }
    }

    private string GetDebuggerDisplay()
    {
        int workersStarted = _workers.Count(it => it is not null);
        int pending = Volatile.Read(ref _pendingJobsCount);

        return $"Status={Status}, Threads={ThreadPoolSize}, WorkersStarted={workersStarted}, PendingJobs={pending}, CancelRequested={CancellationToken.IsCancellationRequested}, Disposed={Volatile.Read(ref _disposedValue)}";
    }

    public override string ToString() => GetDebuggerDisplay();

    #region IDisposable and IAsyncIDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (Volatile.Read(ref _disposedValue))
            return;

        if (disposing)
        {
            StopInternal();

            try
            {
                Task[] startedWorkers = [.. _workers.Where(it => it != null)];

                if (startedWorkers.Length > 0)
                    Task.WhenAll(startedWorkers).Wait();
            }
            catch { } //Ignore exceptions during disposal

            _cancellationTokenSource.Dispose();
        }

        Interlocked.Exchange(ref _disposedValue, true);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        StopInternal();

        try
        {
            Task[] startedWorkers = [.. _workers.Where(it => it != null)];

            if (startedWorkers.Length > 0)
                await Task.WhenAll(startedWorkers).ConfigureAwait(false);
        }
        catch { }

        _cancellationTokenSource.Dispose();
        Interlocked.Exchange(ref _disposedValue, true);
    }

    #endregion
}