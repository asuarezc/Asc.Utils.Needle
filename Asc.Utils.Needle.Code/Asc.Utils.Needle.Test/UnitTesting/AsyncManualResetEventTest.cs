using Asc.Utils.Needle.Implementation;

namespace Asc.Utils.Needle.Test.UnitTesting
{
    public class AsyncManualResetEventTest
    {
        [Fact]
        public void WaitAsync_InitiallySet_CompletesSynchronously()
        {
            var ev = new AsyncManualResetEvent(initialState: true);

            Task wait = ev.WaitAsync();

            Assert.True(wait.IsCompleted);
            Assert.False(wait.IsCanceled);
            Assert.False(wait.IsFaulted);
        }

        [Fact]
        public async Task WaitAsync_WaitsUntilSet()
        {
            var ev = new AsyncManualResetEvent(initialState: false);

            Task waiter = ev.WaitAsync();
            Assert.False(waiter.IsCompleted);

            ev.Set();

            await waiter;
            Assert.True(waiter.IsCompleted);
        }

        [Fact]
        public async Task MultipleWaiters_ProceedAfterSet()
        {
            var ev = new AsyncManualResetEvent(initialState: false);

            Task[] waiters = Enumerable.Range(0, 4).Select(_ => ev.WaitAsync()).ToArray();
            Assert.All(waiters, t => Assert.False(t.IsCompleted));

            ev.Set();

            await Task.WhenAll(waiters);
            Assert.All(waiters, t => Assert.True(t.IsCompleted));
        }

        [Fact]
        public async Task Reset_PreventsNewWaitersUntilSetAgain()
        {
            var ev = new AsyncManualResetEvent(initialState: true);

            // Initially signaled
            Assert.True(ev.WaitAsync().IsCompleted);

            ev.Reset();

            var waiter = ev.WaitAsync();
            Assert.False(waiter.IsCompleted);

            ev.Set();
            await waiter;
        }

        [Fact]
        public void WaitAsync_WithAlreadyCanceledToken_ReturnsCanceledTask()
        {
            var ev = new AsyncManualResetEvent(initialState: false);
            var ct = new CancellationToken(canceled: true);

            Task wait = ev.WaitAsync(ct);

            Assert.True(wait.IsCanceled);
        }

        [Fact]
        public async Task WaitAsync_CanceledWhileWaiting_TaskIsCanceled()
        {
            var ev = new AsyncManualResetEvent(initialState: false);
            using var cts = new CancellationTokenSource();

            Task wait = ev.WaitAsync(cts.Token);
            Assert.False(wait.IsCompleted);

            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await wait.ConfigureAwait(false));
        }

        [Fact]
        public async Task Set_IsIdempotent_AndKeepsSignaledState()
        {
            var ev = new AsyncManualResetEvent(initialState: false);

            ev.Set();
            // multiple waits after Set should complete synchronously
            Assert.True(ev.WaitAsync().IsCompleted);
            Assert.True(ev.WaitAsync().IsCompleted);

            // Set again should not break anything
            ev.Set();
            Assert.True(ev.WaitAsync().IsCompleted);

            // Reset and ensure it blocks again
            ev.Reset();
            var waiter = ev.WaitAsync();
            Assert.False(waiter.IsCompleted);
            ev.Set();
            await waiter;
        }
    }
}
