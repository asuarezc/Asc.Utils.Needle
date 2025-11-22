using Asc.Utils.Needle.Implementation;
using Moq;

namespace Asc.Utils.Needle.Test.UnitTesting
{
    public class NeedleJobProcessorSlimTest
    {
        private static Task WaitWithTimeout(Task task, int ms = 2000) =>
            task.WaitAsync(TimeSpan.FromMilliseconds(ms));

        [Fact]
        public void Start_SetsStatusToRunning()
        {
            var pauseMock = new Mock<IAsyncManualResetEvent>(MockBehavior.Strict);
            pauseMock.Setup(p => p.Set());
            pauseMock.Setup(p => p.Reset());
            pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);

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

            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
            processor.Start();

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            processor.ProcessJob(() => tcs.SetResult(42));

            await WaitWithTimeout(tcs.Task);
            Assert.Equal(42, await tcs.Task);
        }

        [Fact]
        public async Task Pause_PreventsExecution_UntilResume()
        {
            // tcsWait models the event state; Start -> Set() will complete it; Pause -> Reset() will create a new incomplete tcs
            var tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // initially non-signaled; Start will call Set -> will signal
            var pauseMock = new Mock<IAsyncManualResetEvent>();
            pauseMock.Setup(p => p.WaitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) => tcsWait.Task.WaitAsync(ct));
            pauseMock.Setup(p => p.Set()).Callback(() => tcsWait.TrySetResult(true));
            pauseMock.Setup(p => p.Reset()).Callback(() => tcsWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);

            processor.Start(); // Start will call Set() -> signal waiters

            // Now pause: Reset will replace tcsWait with non-completed TCS so workers will block on next loop
            processor.Pause();

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            processor.ProcessJob(() => tcs.SetResult(1));

            // small delay to allow worker to observe paused state
            await Task.Delay(200);
            Assert.False(tcs.Task.IsCompleted);

            // Resume: Set will signal tcsWait and worker proceeds
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

            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
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

            using var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.CancelPendingJobs, pauseMock.Object);
            processor.Start();

            var faultTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            processor.JobFaulted += (_, ex) => faultTcs.TrySetResult(ex);

            var executedSecond = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            // First job throws
            processor.ProcessJob(() => throw new InvalidOperationException("first"));

            // Second job would set this if executed
            processor.ProcessJob(async () =>
            {
                executedSecond.SetResult(1);
                await Task.CompletedTask;
            });

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

            var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
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

            var processor = new NeedleJobProcessorSlim(1, OnJobFailedBehaviour.ContinueRunningPendingJobs, pauseMock.Object);
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
    }
}