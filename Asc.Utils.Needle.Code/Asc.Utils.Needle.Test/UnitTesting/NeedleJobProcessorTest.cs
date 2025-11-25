using Asc.Utils.Needle.Implementation;
using Moq;
using System.Collections.Concurrent;

namespace Asc.Utils.Needle.Test.UnitTesting;

public class NeedleJobProcessorTest
{
    #region Same tests as in NeedleJobProcessorSlimTest

    private static Task WaitWithTimeout(Task task, int ms = 2000) =>
        task.WaitAsync(TimeSpan.FromMilliseconds(ms));

    [Fact]
    public void Start_SetsStatusToRunning()
    {
        var pauseMock = new Mock<IAsyncManualResetEvent>(MockBehavior.Strict);
        pauseMock.Setup(p => p.Set());
        pauseMock.Setup(p => p.Reset());
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);

        Assert.Equal(NeedleJobProcessorStatus.Stopped, processor.Status);

        processor.Start();

        Assert.Equal(NeedleJobProcessorStatus.Running, processor.Status);

        pauseMock.Verify(p => p.Set(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessJob_Action_IsExecuted()
    {
        // Pause event initially signaled
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);

        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        processor.Start();

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        processor.ProcessJob(() => tcs.SetResult(42));

        await WaitWithTimeout(tcs.Task);
        Assert.Equal(42, await tcs.Task);
    }

    [Fact]
    public async Task Pause_PreventsExecution_UntilResume()
    {
        // tcsWait models the event state; Start -> Set() will complete it; Reset() will create a new incomplete tcs
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);

        processor.Start();

        // Blocker job to occupy the worker
        var blockerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblocker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        processor.ProcessJob(async () =>
        {
            blockerStarted.TrySetResult(true);
            await unblocker.Task;
        });

        // wait until worker started the blocker job
        await WaitWithTimeout(blockerStarted.Task);

        // Now pause — worker is busy and won't read the next job
        processor.Pause();

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.ProcessJob(() => tcs.SetResult(1));

        // short delay to ensure the enqueued job is pending while worker is busy
        await Task.Delay(50);
        Assert.False(tcs.Task.IsCompleted);

        // Unblock the blocker; worker should observe Pause and wait
        unblocker.TrySetResult(true);

        // give small time to reach wait; job still should be pending
        await Task.Delay(50);
        Assert.False(tcs.Task.IsCompleted);

        // Resume and verify job runs
        processor.Resume();
        await WaitWithTimeout(tcs.Task);
        Assert.True(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task JobFaulted_EventIsRaisedOnJobException()
    {
        // use signaled pause so worker runs immediately
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);

        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        processor.Start();

        var tcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.JobFaulted += (_, ex) => tcs.TrySetResult(ex);

        processor.ProcessJob(() => throw new InvalidOperationException("boom"));

        await WaitWithTimeout(tcs.Task);
        var ex = await tcs.Task;
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task CancelPendingJobs_ClearsPendingJobsWhenOneFails()
    {
        // signaled pause so worker runs
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);

        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.CancelPendingJobs, pauseMock.Object);
        processor.Start();

        var faultTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.JobFaulted += (_, ex) => faultTcs.TrySetResult(ex);

        var executedSecond = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Use a blocker so we can deterministically control when the failing job is processed
        var blockerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblocker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Enqueue blocker first to occupy the single worker
        processor.ProcessJob(async () =>
        {
            blockerStarted.TrySetResult(true);
            await unblocker.Task;
        });

        // Wait until the worker has started the blocker
        await WaitWithTimeout(blockerStarted.Task);

        // Enqueue the failing job and the job that should be cleared
        processor.ProcessJob(() => throw new InvalidOperationException("first"));
        processor.ProcessJob(async () =>
        {
            executedSecond.TrySetResult(1);
            await Task.CompletedTask;
        });

        // Now allow the worker to proceed to process the failing job
        unblocker.TrySetResult(true);

        // wait for the fault to be raised
        await WaitWithTimeout(faultTcs.Task);

        // give some time for the system to clear pending jobs
        await Task.Delay(200);

        Assert.False(executedSecond.Task.IsCompleted);
    }

    [Fact]
    public void Dispose_PreventsFurtherProcessing()
    {
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);
        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        processor.Start();
        processor.Dispose();

        Assert.Throws<ObjectDisposedException>(() => processor.ProcessJob(() => { }));
        Assert.Throws<ObjectDisposedException>(() => processor.ProcessJob(async () => await Task.CompletedTask));
    }

    [Fact]
    public async Task DisposeAsync_WaitsForWorkersToFinish()
    {
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);
        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        processor.Start();

        // enqueue a job that completes after a short delay
        processor.ProcessJob(async () =>
        {
            await Task.Delay(100).ConfigureAwait(false);
        });

        // dispose async should wait for enqueued job to complete
        var disposeTask = processor.DisposeAsync().AsTask();

        // wait the job completes
        await WaitWithTimeout(disposeTask, 3000);
    }

    #endregion

    #region Tests specific to NeedleJobProcessor (counters & INotifyPropertyChanged)

    [Fact]
    public async Task ProcessJob_Action_IncrementsCountersAndNotifies()
    {
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);
        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        var changed = new ConcurrentBag<string>();
        processor.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        processor.Start();

        var jobDone = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.ProcessJob(() => jobDone.SetResult(1));

        await WaitWithTimeout(jobDone.Task);

        Assert.Equal(1, processor.TotalJobsCount);
        Assert.Equal(1, processor.SuccessfullyCompletedJobsCount);
        Assert.Contains(nameof(processor.TotalJobsCount), changed);
        Assert.Contains(nameof(processor.SuccessfullyCompletedJobsCount), changed);
    }

    [Fact]
    public async Task ProcessJob_Func_IncrementsCountersAndNotifies()
    {
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);
        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        var changed = new ConcurrentBag<string>();
        processor.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        processor.Start();

        var jobDone = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.ProcessJob(async () =>
        {
            jobDone.SetResult(1);
            await Task.CompletedTask;
        });

        await WaitWithTimeout(jobDone.Task);

        Assert.Equal(1, processor.TotalJobsCount);
        Assert.Equal(1, processor.SuccessfullyCompletedJobsCount);
        Assert.Contains(nameof(processor.TotalJobsCount), changed);
        Assert.Contains(nameof(processor.SuccessfullyCompletedJobsCount), changed);
    }

    [Fact]
    public async Task FaultedJob_IncrementsFaultedCounterAndNotifies()
    {
        var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcsWait.SetResult(true);
        var pauseMock = new Mock<IAsyncManualResetEvent>();
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
        pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
        pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        var changed = new ConcurrentBag<string>();
        processor.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        processor.Start();

        var faulted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.JobFaulted += (_, ex) => faulted.TrySetResult(ex);

        processor.ProcessJob(() => throw new InvalidOperationException("boom"));

        await WaitWithTimeout(faulted.Task);

        Assert.Equal(1, processor.FaultedJobsCount);
        Assert.Contains(nameof(processor.FaultedJobsCount), changed);
    }

    [Fact]
    public void StartPauseResume_RaisesStatusPropertyChanged()
    {
        var pauseMock = new Mock<IAsyncManualResetEvent>(MockBehavior.Strict);
        pauseMock.Setup(p => p.Set());
        pauseMock.Setup(p => p.Reset());
        pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        using var processor = new JobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
        var changed = new List<string>();
        processor.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        processor.Start();
        processor.Pause();
        processor.Resume();

        Assert.Contains(nameof(processor.Status), changed);
    }

    #endregion
}
