using Asc.Utils.Needle.Implementation;

namespace Asc.Utils.Needle.Test.IntegrationTesting
{
    public class NeedleJobProcessorSlimWithAsyncManualResetEventTest
    {
        private static Task WaitWithTimeout(Task task, int ms = 2000) =>
            task.WaitAsync(TimeSpan.FromMilliseconds(ms));

        [Fact]
        public void Start_WithRealAsyncManualResetEvent_SetsStatusToRunning()
        {
            var pause = new AsyncManualResetEvent(initialState: false);
            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pause);

            Assert.Equal(NeedleJobProcessorStatus.Stopped, processor.Status);

            processor.Start();

            Assert.Equal(NeedleJobProcessorStatus.Running, processor.Status);
        }

        [Fact]
        public async Task ProcessJob_WithRealAsyncManualResetEvent_ActionIsExecuted()
        {
            var pause = new AsyncManualResetEvent(initialState: false);
            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pause);

            processor.Start();

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            processor.ProcessJob(() => tcs.TrySetResult(123));

            await WaitWithTimeout(tcs.Task);
            Assert.Equal(123, await tcs.Task);
        }

        [Fact]
        public async Task Pause_WithRealEvent_BlocksUntilResume()
        {
            var pause = new AsyncManualResetEvent(initialState: false);
            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pause);

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
            var pause = new AsyncManualResetEvent(initialState: false);
            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pause);

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
            var pause = new AsyncManualResetEvent(initialState: false);
            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.CancelPendingJobs, pause);

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
            var pause = new AsyncManualResetEvent(initialState: false);
            var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pause);

            processor.Start();

            processor.ProcessJob(async () =>
            {
                await Task.Delay(100).ConfigureAwait(false);
            });

            var disposeTask = processor.DisposeAsync().AsTask();

            await WaitWithTimeout(disposeTask);
        }
    }
}
