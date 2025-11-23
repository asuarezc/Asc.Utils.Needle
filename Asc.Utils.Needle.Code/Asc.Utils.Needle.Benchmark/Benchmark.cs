using BenchmarkDotNet.Attributes;

namespace Asc.Utils.Needle.Benchmark
{
    [MemoryDiagnoser]
    public class Benchmark
    {
        private INeedleWorkerSlim? semaphoreWorker;
        private INeedleWorkerSlim? parallelWorker;
        private INeedleJobProcessorSlim? jobProcessor;
        private TaskCompletionSource<bool>? taskCompletionSource;
        private int completedJobs;

        [Params(10, 50, 100)]
        public int JobsCount { get; set; }

        #region Setups

        [IterationSetup(Target = "SemaphoreWorkerSlim")]
        public void IterationSetupForSemaphoreWorkerSlim()
        {
            semaphoreWorker = Pincushion.Instance.GetSemaphoreWorkerSlim();

            for (int i = 0; i < JobsCount; i++)
                semaphoreWorker.AddJob(async () => await Task.Delay(1));
        }

        [IterationSetup(Target = "ParallelWorkerSlim")]
        public void IterationSetupForParallelWorkerSlim()
        {
            parallelWorker = Pincushion.Instance.GetParallelWorkerSlim();

            for (int i = 0; i < JobsCount; i++)
                parallelWorker.AddJob(async () => await Task.Delay(1));
        }

        [IterationSetup(Target = "JobProcessorSlim")]
        public void IterationSetupForJobProcessorSlim()
        {
            completedJobs = 0;
            taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            jobProcessor = Pincushion.Instance.GetNeedleJobProcessorSlim();

            for (int i = 0; i < JobsCount; i++)
            {
                jobProcessor.ProcessJob(async () =>
                {
                    await Task.Delay(1);

                    if (Interlocked.Increment(ref completedJobs) == JobsCount)
                        taskCompletionSource.TrySetResult(true);
                });
            }
        }

        #endregion

        [Benchmark(Description = "Sequential")]
        public async Task Sequential()
        {
            for (int i = 0; i < JobsCount; i++)
                await Task.Delay(1);
        }

        [Benchmark(Description = "SemaphoreWorkerSlim")]
        public async Task SemaphoreWorkerSlim()
        {
            await semaphoreWorker!.RunAsync();
        }

        [Benchmark(Description = "ParallelWorkerSlim")]
        public async Task ParallelWorkerSlim()
        {
            await parallelWorker!.RunAsync();
        }

        [Benchmark(Description = "JobProcessorSlim")]
        public async Task JobProcessorSlim()
        {
            jobProcessor!.Start();
            await taskCompletionSource!.Task;
        }

        #region Cleanups

        [IterationCleanup(Target = "SemaphoreWorkerSlim")]
        public void IterationCleanupForSemaphoreWorkerSlim()
        {
            semaphoreWorker!.Dispose();
            semaphoreWorker = null;
        }

        [IterationCleanup(Target = "ParallelWorkerSlim")]
        public void IterationCleanupForParallelWorkerSlim()
        {
            parallelWorker!.Dispose();
            parallelWorker = null;
        }

        [IterationCleanup(Target = "JobProcessorSlim")]
        public void IterationCleanupForJobProcessorSlim()
        {
            jobProcessor!.Dispose();
            jobProcessor = null;
        }

        #endregion
    }
}