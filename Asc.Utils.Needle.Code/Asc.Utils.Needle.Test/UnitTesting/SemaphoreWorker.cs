using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Test.UnitTesting;

public class SemaphoreWorker
{
    [Fact]
    public void MaxThreadsProperty_DefaultValue()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        Assert.Equal(Environment.ProcessorCount / 2, worker.MaxThreads);

        worker.Dispose();
    }

    [Fact]
    public void MaxThreadsProperty_CustomValue()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(2);
        Assert.Equal(2, worker.MaxThreads);

        worker.Dispose();
    }

    [Fact]
    public void TotalJobsCount_WhenNoJobsAdded()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        Assert.Equal(0, Needle.Instance.GetSemaphoreWorker().TotalJobsCount);

        worker.Dispose();
    }

    [Fact]
    public void TotalJobsCount_WhenAddedSomeJobs()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();

        worker.AddJob(() => Console.WriteLine($"Just testing {nameof(TotalJobsCount_WhenAddedSomeJobs)}"));
        worker.AddJob(() => Console.WriteLine("Whatever..."));
        worker.AddJob(() => Console.WriteLine("Ignore this!"));

        Assert.Equal(3, worker.TotalJobsCount);
        worker.Dispose();
    }

    [Fact]
    public void CompletedJobsCount_WhenNoJobsRunnedYet()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        Assert.Equal(0, worker.CompletedJobsCount);

        worker.Dispose();
    }

    [Fact]
    public void CompletedJobsCount_WhenSomeJobsHaveBeenCompletedButNotOthers()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);

        worker.AddJob(
            () => Console.WriteLine($"Just testing {nameof(CompletedJobsCount_WhenSomeJobsHaveBeenCompletedButNotOthers)}"),
            JobPriority.Highest
        );

        worker.AddJob(async () =>
        {
            await Task.Delay(100);
            Assert.Equal(1, worker.CompletedJobsCount);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        worker.RunAsync().Wait();
        worker.Dispose();
    }

    [Fact]
    public void Progress_WhenNoJobsRunnedYet()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        Assert.Equal(0, worker.Progress);

        worker.Dispose();
    }

    [Fact]
    public void Progress_WhenSomeJobsHaveBeenCompletedButNotOthers()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);

        worker.AddJob(
            () => Console.WriteLine($"Just testing {nameof(Progress_WhenSomeJobsHaveBeenCompletedButNotOthers)}"),
            JobPriority.Highest
        );

        worker.AddJob(async () =>
        {
            await Task.Delay(100);
            Assert.Equal(50, worker.Progress);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        worker.RunAsync().Wait();
        worker.Dispose();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenDefaultValue()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        Assert.True(worker.CancelPendingJobsIfAnyOtherFails);

        worker.Dispose();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenCustomValue()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(cancelPendingJobsIfAnyOtherFails: false);
        Assert.False(worker.CancelPendingJobsIfAnyOtherFails);

        worker.Dispose();
    }

    [Fact]
    public void IsRunning_WhenTrue()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);

        worker.AddJob(
            () => Console.WriteLine($"Just testing {nameof(IsRunning_WhenTrue)}"),
            JobPriority.Highest
        );

        worker.AddJob(async () =>
        {
            await Task.Delay(100);
            Assert.True(worker.IsRunning);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        worker.RunAsync().Wait();
        worker.Dispose();
    }

    [Fact]
    public void IsRunning_WhenFalseSinceNotStartedYet()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        Assert.False(worker.IsRunning);

        worker.Dispose();
    }

    [Fact]
    public void IsRunning_WhenFalseSinceWorkIsAlreadyDone()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);

        worker.AddJob(() => Console.WriteLine($"Just testing {nameof(IsRunning_WhenFalseSinceWorkIsAlreadyDone)}"));
        worker.RunAsync().Wait();
        Assert.False(worker.IsRunning);

        worker.Dispose();
    }

    [Fact]
    public void Completed()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);

        static void onCompleted(object? sender, EventArgs e) { Assert.True(true); };

        worker.Completed += onCompleted;

        worker.AddJob(() => Console.WriteLine($"Just testing {nameof(Completed)}"));
        worker.RunAsync().Wait();

        worker.Completed -= onCompleted;
        worker.Dispose();
    }

    [Fact]
    public void JobFaulted()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);
        bool faulted = false;
        void onFaulted(object? sender, Exception ex) { faulted = true; };

        worker.JobFaulted += onFaulted;

        worker.AddJob(() => throw new NotImplementedException());
        Assert.Throws<AggregateException>(() => worker.RunAsync().Wait());
        Assert.True(faulted);

        worker.JobFaulted -= onFaulted;
        worker.Dispose();
    }

    [Fact]
    public void Canceled()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);
        bool canceled = false;
        void onCanceled(object? sender, EventArgs e) { canceled = true; };

        worker.Canceled += onCanceled;

        worker.AddJob(worker.RequestCancellation, JobPriority.Highest);
        worker.AddJob(() => { canceled = false; }, JobPriority.Lowest);
        worker.RunAsync().Wait();
        Assert.True(canceled);

        worker.Canceled -= onCanceled;
        worker.Dispose();
    }

    [Fact]
    public void BeginRung()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);
        bool completed = false;
        void onCompleted(object? sender, EventArgs e) { completed = true; };

        worker.Completed += onCompleted;

        worker.AddJob(() => Console.WriteLine($"Just testing {nameof(BeginRung)}"));
        worker.BeginRun();
        Task.Delay(100).Wait();
        Assert.True(completed);

        worker.Completed -= onCompleted;
        worker.Dispose();
    }

    [Fact]
    public void IntendedUse()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();

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
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();

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
    public void NotifyPropertyChanged_Test()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(1);

        worker.AddJob(() => Console.WriteLine($"Just testing {nameof(NotifyPropertyChanged_Test)}"));
        worker.AddJob(() => Console.WriteLine("Ignore this. Nothing to see"));
        worker.AddJob(() => Console.WriteLine("Told you. This is only testing"));
        worker.AddJob(() => Console.WriteLine("Ok, nevermind. Do what you want"));

        void onPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e is null || string.IsNullOrEmpty(e.PropertyName))
                return;

            if (e.PropertyName == nameof(ISemaphoreWorker.Progress))
            {
                Assert.True(
                    worker.Progress == 0
                    || worker.Progress == 25
                    || worker.Progress == 50
                    || worker.Progress == 75
                    || worker.Progress == 100
                );
            }

            if (e.PropertyName == nameof(ISemaphoreWorker.CompletedJobsCount))
            {
                Assert.True(
                    worker.CompletedJobsCount == 0
                    || worker.CompletedJobsCount == 1
                    || worker.CompletedJobsCount == 2
                    || worker.CompletedJobsCount == 3
                    || worker.CompletedJobsCount == 4
                );
            }
        }

        worker.PropertyChanged += onPropertyChanged;

        worker.RunAsync().Wait();

        worker.PropertyChanged -= onPropertyChanged;
        worker.Dispose();
    }

    [Fact]
    public void DisposeTest()
    {
        ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();
        worker.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { int completed = worker.CompletedJobsCount; });
    }
}
