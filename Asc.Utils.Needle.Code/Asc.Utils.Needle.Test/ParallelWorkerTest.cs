using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Test;

public class ParallelWorkerTest
{
    [Fact]
    public void TotalJobsCount_WhenNoJobsAdded()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();
        Assert.Equal(0, Needle.Instance.GetSemaphoreWorker().TotalJobsCount);

        worker.Dispose();
    }

    [Fact]
    public void TotalJobsCount_WhenAddedSomeJobs()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();

        worker.AddJob(() => Console.WriteLine($"Just testing {nameof(TotalJobsCount_WhenAddedSomeJobs)}"));
        worker.AddJob(() => Console.WriteLine("Whatever..."));
        worker.AddJob(() => Console.WriteLine("Ignore this!"));

        Assert.Equal(3, worker.TotalJobsCount);
        worker.Dispose();
    }

    [Fact]
    public void CompletedJobsCount_WhenNoJobsRunnedYet()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();
        Assert.Equal(0, worker.CompletedJobsCount);

        worker.Dispose();
    }

    [Fact]
    public void Progress_WhenNoJobsRunnedYet()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();
        Assert.Equal(0, worker.Progress);

        worker.Dispose();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenDefaultValue()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();
        Assert.True(worker.CancelPendingJobsIfAnyOtherFails);

        worker.Dispose();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenCustomValue()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker(cancelPendingJobsIfAnyOtherFails: false);
        Assert.False(worker.CancelPendingJobsIfAnyOtherFails);

        worker.Dispose();
    }

    [Fact]
    public void IsRunning_WhenFalseSinceNotStartedYet()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();
        Assert.False(worker.IsRunning);

        worker.Dispose();
    }

    [Fact]
    public void IntendedUse()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();

        //Ten jobs (total delay is 100 if one thread only)
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));

        Stopwatch stopwatch = Stopwatch.StartNew();
        worker.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
        worker.Dispose();
    }

    [Fact]
    public void TwoBatchesOfJobs()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();

        //Ten jobs (total delay is 100 if one thread only)
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));

        Stopwatch stopwatch = Stopwatch.StartNew();
        worker.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);

        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));
        worker.AddJob(async () => await Task.Delay(10));

        stopwatch.Restart();
        worker.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
        worker.Dispose();
    }

    [Fact]
    public void DisposeTest()
    {
        IParallelWorker worker = Needle.Instance.GetParallelWorker();
        worker.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { int completed = worker.CompletedJobsCount; });
    }
}
