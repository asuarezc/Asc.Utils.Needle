using Asc.Utils.Needle.Implementation;
using System.Collections.Concurrent;

namespace Asc.Utils.Needle.Test.IntegrationTesting;

public class NeedleJobProcessorWithAsyncManualResetEventTest
{
    #region NeedleJobProcessorSlim tests

    private static Task WaitWithTimeout(Task task, int ms = 2000) =>
        task.WaitAsync(TimeSpan.FromMilliseconds(ms));

    [Fact]
    public void Start_WithRealAsyncManualResetEvent_SetsStatusToRunning()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

        Assert.Equal(NeedleJobProcessorStatus.Stopped, processor.Status);

        processor.Start();

        Assert.Equal(NeedleJobProcessorStatus.Running, processor.Status);
    }

    [Fact]
    public async Task ProcessJob_WithRealAsyncManualResetEvent_ActionIsExecuted()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

        processor.Start();

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        processor.ProcessJob(() => tcs.TrySetResult(123));

        await WaitWithTimeout(tcs.Task);
        Assert.Equal(123, await tcs.Task);
    }

    [Fact]
    public async Task Pause_WithRealEvent_BlocksUntilResume()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

        processor.Start(); // Start sets the event -> workers proceed

        // Pause to block workers
        processor.Pause();

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.ProcessJob(() => tcs.TrySetResult(7));

        // small delay to allow worker to reach wait, should still be blocked
        await Task.Delay(150);
        Assert.False(tcs.Task.IsCompleted);

        // Resume and verify job runs
        processor.Resume();
        await WaitWithTimeout(tcs.Task);
        Assert.True(tcs.Task.IsCompleted);
        Assert.Equal(7, await tcs.Task);
    }

    [Fact]
    public async Task Pause_PreventsExecution_UntilResume()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

        processor.Start();

        // Blocker job to occupy the worker
        var blockerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblocker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        processor.ProcessJob(async () =>
        {
            blockerStarted.TrySetResult(true);
            await unblocker.Task.ConfigureAwait(false);
        });

        // wait until worker started the blocker job
        await WaitWithTimeout(blockerStarted.Task);

        // Now pause — worker is busy and won't read the next job
        processor.Pause();

        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.ProcessJob(() => tcs.TrySetResult(7));

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
        Assert.Equal(7, await tcs.Task);
    }

    [Fact]
    public async Task CancelPendingJobs_WithRealEvent_ClearsPendingJobsWhenOneFails()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.CancelPendingJobs);

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
            await unblocker.Task.ConfigureAwait(false);
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
    public async Task DisposeAsync_WithRealEvent_WaitsForInFlightJobs()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

        processor.Start();

        processor.ProcessJob(async () =>
        {
            await Task.Delay(100).ConfigureAwait(false);
        });

        var disposeTask = processor.DisposeAsync().AsTask();

        await WaitWithTimeout(disposeTask);
    }

    // Integration: high-throughput realistic workload
    [Fact]
    public async Task HighThroughput_ProcessAllJobs()
    {
        const int threadPoolSize = 4;
        const int totalJobs = 1000;

        using var processor = Pincushion.Instance.GetNeedleJobProcessor(threadPoolSize, OnJobFailedBehaviour.ContinueRunningPendingJobs);

        var bag = new ConcurrentBag<int>();

        processor.Start();

        // produce jobs concurrently
        var producers = new List<Task>();
        int producersCount = Environment.ProcessorCount;
        int jobsPerProducer = totalJobs / Math.Max(1, producersCount);

        for (int p = 0; p < producersCount; p++)
        {
            int start = p * jobsPerProducer;
            int end = (p == producersCount - 1) ? totalJobs : start + jobsPerProducer;

            producers.Add(Task.Run(() =>
            {
                for (int i = start; i < end; i++)
                {
                    int capture = i;
                    processor.ProcessJob(() => { bag.Add(capture); return Task.CompletedTask; });
                }
            }));
        }

        await Task.WhenAll(producers);

        // wait until all jobs processed or timeout
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (bag.Count < totalJobs && sw.ElapsedMilliseconds < 5000)
            await Task.Delay(20);

        // stop processor and wait
        await processor.DisposeAsync();

        Assert.Equal(totalJobs, bag.Count);
    }

    [Fact]
    public async Task CancelDuringHighLoad_ClearsPendingJobs()
    {
        const int threadPoolSize = 1; // single worker to ensure predictable ordering
        const int totalJobs = 500;

        using var processor = Pincushion.Instance.GetNeedleJobProcessor(threadPoolSize, OnJobFailedBehaviour.CancelPendingJobs);

        processor.Start();

        var executed = new ConcurrentBag<int>();
        var faultTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        processor.JobFaulted += (_, ex) => faultTcs.TrySetResult(ex);

        // Enqueue a blocker to occupy worker briefly so many jobs queue up
        var blockerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unblocker = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        processor.ProcessJob(async () =>
        {
            blockerStarted.TrySetResult(true);
            await unblocker.Task;
        });

        await WaitWithTimeout(blockerStarted.Task);

        // Enqueue a failing job followed by many jobs
        processor.ProcessJob(() => throw new InvalidOperationException("boom"));

        for (int i = 0; i < totalJobs; i++)
        {
            int capture = i;
            processor.ProcessJob(async () => { executed.Add(capture); await Task.CompletedTask; });
        }

        // allow failing job to run
        unblocker.TrySetResult(true);

        // wait fault
        await WaitWithTimeout(faultTcs.Task);

        // give some time for clearing
        await Task.Delay(200);

        // stop processor
        await processor.DisposeAsync();

        // Since OnJobFailedBehaviour is CancelPendingJobs and single worker, none of the later jobs should have executed
        Assert.True(executed.IsEmpty);
    }

    #endregion

    #region Integration tests specific to NeedleJobProcessor (counters & INotifyPropertyChanged)

    [Fact]
    public async Task ProcessJob_Action_IncrementsCountersAndNotifies_Integration()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

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
    public async Task ProcessJob_Func_IncrementsCountersAndNotifies_Integration()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

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
    public async Task FaultedJob_IncrementsFaultedCounterAndNotifies_Integration()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);

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
    public void StartPauseResume_RaisesStatusPropertyChanged_Integration()
    {
        using var processor = Pincushion.Instance.GetNeedleJobProcessor(1, OnJobFailedBehaviour.ContinueRunningPendingJobs);
        var changed = new List<string>();
        processor.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        processor.Start();
        processor.Pause();
        processor.Resume();

        Assert.Contains(nameof(processor.Status), changed);
    }

    #endregion
}
