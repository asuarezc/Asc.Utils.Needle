using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Test;

public class NeedleWorkerTest
{
    [Fact]
    public void MaxThreadsProperty_DefaultValue()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        Assert.Equal(Environment.ProcessorCount, needle.MaxThreads);

        needle.Dispose();
    }

    [Fact]
    public void MaxThreadsProperty_CustomValue()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(2);
        Assert.Equal(2, needle.MaxThreads);

        needle.Dispose();
    }

    [Fact]
    public void TotalJobsCount_WhenNoJobsAdded()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        Assert.Equal(0, Pincushion.Instance.GetNeedle().TotalJobsCount);

        needle.Dispose();
    }

    [Fact]
    public void TotalJobsCount_WhenAddedSomeJobs()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();

        needle.AddJob(() => Console.WriteLine($"Just testing {nameof(TotalJobsCount_WhenAddedSomeJobs)}"));
        needle.AddJob(() => Console.WriteLine("Whatever..."));
        needle.AddJob(() => Console.WriteLine("Ignore this!"));

        Assert.Equal(3, needle.TotalJobsCount);
        needle.Dispose();
    }

    [Fact]
    public void CompletedJobsCount_WhenNoJobsRunnedYet()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        Assert.Equal(0, needle.CompletedJobsCount);

        needle.Dispose();
    }

    [Fact]
    public void CompletedJobsCount_WhenSomeJobsHaveBeenCompletedButNotOthers()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);

        needle.AddJob(
            () => Console.WriteLine($"Just testing {nameof(CompletedJobsCount_WhenSomeJobsHaveBeenCompletedButNotOthers)}"),
            JobPriority.Highest
        );

        needle.AddJob(async () => {
            await Task.Delay(100);
            Assert.Equal(1, needle.CompletedJobsCount);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        needle.RunAsync().Wait();
        needle.Dispose();
    }

    [Fact]
    public void Progress_WhenNoJobsRunnedYet()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        Assert.Equal(0, needle.Progress);

        needle.Dispose();
    }

    [Fact]
    public void Progress_WhenSomeJobsHaveBeenCompletedButNotOthers()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);

        needle.AddJob(
            () => Console.WriteLine($"Just testing {nameof(Progress_WhenSomeJobsHaveBeenCompletedButNotOthers)}"),
            JobPriority.Highest
        );

        needle.AddJob(async () => {
            await Task.Delay(100);
            Assert.Equal(50, needle.Progress);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        needle.RunAsync().Wait();
        needle.Dispose();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenDefaultValue()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        Assert.True(needle.CancelPendingJobsIfAnyOtherFails);

        needle.Dispose();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenCustomValue()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(cancelPendingJobsIfAnyOtherFails: false);
        Assert.False(needle.CancelPendingJobsIfAnyOtherFails);

        needle.Dispose();
    }

    [Fact]
    public void IsRunning_WhenTrue()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);

        needle.AddJob(
            () => Console.WriteLine($"Just testing {nameof(IsRunning_WhenTrue)}"),
            JobPriority.Highest
        );

        needle.AddJob(async () => {
            await Task.Delay(100);
            Assert.True(needle.IsRunning);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        needle.RunAsync().Wait();
        needle.Dispose();
    }

    [Fact]
    public void IsRunning_WhenFalseSinceNotStartedYet()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        Assert.False(needle.IsRunning);

        needle.Dispose();
    }

    [Fact]
    public void IsRunning_WhenFalseSinceWorkIsAlreadyDone()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);

        needle.AddJob(() => Console.WriteLine($"Just testing {nameof(IsRunning_WhenFalseSinceWorkIsAlreadyDone)}"));
        needle.RunAsync().Wait();
        Assert.False(needle.IsRunning);

        needle.Dispose();
    }

    [Fact]
    public void Completed()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);

        static void onCompleted(object? sender, EventArgs e) { Assert.True(true); };

        needle.Completed += onCompleted;

        needle.AddJob(() => Console.WriteLine($"Just testing {nameof(Completed)}"));
        needle.RunAsync().Wait();

        needle.Completed -= onCompleted;
        needle.Dispose();
    }

    [Fact]
    public void JobFaulted()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);
        bool faulted = false;
        void onFaulted(object? sender, Exception ex) { faulted = true; };

        needle.JobFaulted += onFaulted;

        needle.AddJob(() => throw new NotImplementedException());
        Assert.Throws<AggregateException>(() => needle.RunAsync().Wait());
        Assert.True(faulted);

        needle.JobFaulted -= onFaulted;
        needle.Dispose();
    }

    [Fact]
    public void Canceled()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);
        bool canceled = false;
        void onCanceled(object? sender, EventArgs e) { canceled = true; };

        needle.Canceled += onCanceled;

        needle.AddJob(needle.RequestCancellation, JobPriority.Highest);
        needle.AddJob(() => { canceled = false; }, JobPriority.Lowest);
        needle.RunAsync().Wait();
        Assert.True(canceled);

        needle.Canceled -= onCanceled;
        needle.Dispose();
    }

    [Fact]
    public void BeginRung()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);
        bool completed = false;
        void onCompleted(object? sender, EventArgs e) { completed = true; };

        needle.Completed += onCompleted;

        needle.AddJob(() => Console.WriteLine($"Just testing {nameof(BeginRung)}"));
        needle.BeginRun();
        Task.Delay(100).Wait();
        Assert.True(completed);

        needle.Completed -= onCompleted;
        needle.Dispose();
    }

    [Fact]
    public void IntendedUse()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();

        //Ten jobs (total delay is 100 if one thread only)
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));

        Stopwatch stopwatch = Stopwatch.StartNew();
        needle.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
        needle.Dispose();
    }

    [Fact]
    public void TwoBatchesOfJobs()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();

        //Ten jobs (total delay is 100 if one thread only)
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));

        Stopwatch stopwatch = Stopwatch.StartNew();
        needle.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);

        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));
        needle.AddJob(async () => await Task.Delay(10));

        stopwatch.Restart();
        needle.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
        needle.Dispose();
    }

    [Fact]
    public void NotifyPropertyChanged_Test()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle(1);

        needle.AddJob(() => Console.WriteLine($"Just testing {nameof(NotifyPropertyChanged_Test)}"));
        needle.AddJob(() => Console.WriteLine("Ignore this. Nothing to see"));
        needle.AddJob(() => Console.WriteLine("Told you. This is only testing"));
        needle.AddJob(() => Console.WriteLine("Ok, nevermind. Do what you want"));

        void onPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e is null || string.IsNullOrEmpty(e.PropertyName))
                return;

            if (e.PropertyName == nameof(INeedleWorker.Progress))
            {
                Assert.True(
                    needle.Progress == 0
                    || needle.Progress == 25
                    || needle.Progress == 50
                    || needle.Progress == 75
                    || needle.Progress == 100
                );
            }

            if (e.PropertyName == nameof(INeedleWorker.CompletedJobsCount))
            {
                Assert.True(
                    needle.CompletedJobsCount == 0
                    || needle.CompletedJobsCount == 1
                    || needle.CompletedJobsCount == 2
                    || needle.CompletedJobsCount == 3
                    || needle.CompletedJobsCount == 4
                );
            }
        }

        needle.PropertyChanged += onPropertyChanged;

        needle.RunAsync().Wait();

        needle.PropertyChanged -= onPropertyChanged;
        needle.Dispose();
    }

    [Fact]
    public void DisposeTest()
    {
        INeedleWorker needle = Pincushion.Instance.GetNeedle();
        needle.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { int completed = needle.CompletedJobsCount; });
    }

    //AddJob (both), RequestCancellation() and RunAsyn/( are tested in previous test methods.
}
