using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Asc.Utils.Needle.Test;

public class NeddleWorkerTest
{
    [Fact]
    public void MaxThreadsProperty_DefaultValue()
    {
        Assert.Equal(3, Pincushion.Instance.GetNeedle().MaxThreads);
    }

    [Fact]
    public void MaxThreadsProperty_CustomValue()
    {
        Assert.Equal(2, Pincushion.Instance.GetNeedle(2).MaxThreads);
    }

    [Fact]
    public void TotalJobsCount_WhenNoJobsAdded()
    {
        Assert.Equal(0, Pincushion.Instance.GetNeedle().TotalJobsCount);
    }

    [Fact]
    public void TotalJobsCount_WhenAddedSomeJobs()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle();

        neddle.AddJob(() => Console.WriteLine($"Just testing {nameof(TotalJobsCount_WhenAddedSomeJobs)}"));
        neddle.AddJob(() => Console.WriteLine("Whatever..."));
        neddle.AddJob(() => Console.WriteLine("Ignore this!"));

        Assert.Equal(3, neddle.TotalJobsCount);
    }

    [Fact]
    public void CompletedJobsCount_WhenNoJobsRunnedYet()
    {
        Assert.Equal(0, Pincushion.Instance.GetNeedle().CompletedJobsCount);
    }

    [Fact]
    public void CompletedJobsCount_WhenSomeJobsHaveBeenCompletedButNotOthers()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);

        neddle.AddJob(
            () => Console.WriteLine($"Just testing {nameof(CompletedJobsCount_WhenSomeJobsHaveBeenCompletedButNotOthers)}"),
            JobPriority.Highest
        );

        neddle.AddJob(async () => {
            await Task.Delay(100);
            Assert.Equal(1, neddle.CompletedJobsCount);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        neddle.RunAsync().Wait();
    }

    [Fact]
    public void Progress_WhenNoJobsRunnedYet()
    {
        Assert.Equal(0, Pincushion.Instance.GetNeedle().Progress);
    }

    [Fact]
    public void Progress_WhenSomeJobsHaveBeenCompletedButNotOthers()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);

        neddle.AddJob(
            () => Console.WriteLine($"Just testing {nameof(Progress_WhenSomeJobsHaveBeenCompletedButNotOthers)}"),
            JobPriority.Highest
        );

        neddle.AddJob(async () => {
            await Task.Delay(100);
            Assert.Equal(50, neddle.Progress);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        neddle.RunAsync().Wait();
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenDefaultValue()
    {
        Assert.True(Pincushion.Instance.GetNeedle().CancelPendingJobsIfAnyOtherFails);
    }

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails_WhenCustomValue()
    {
        Assert.False(Pincushion.Instance.GetNeedle(cancelPendingJobsIfAnyOtherFails: false).CancelPendingJobsIfAnyOtherFails);
    }

    [Fact]
    public void IsRunning_WhenTrue()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);

        neddle.AddJob(
            () => Console.WriteLine($"Just testing {nameof(IsRunning_WhenTrue)}"),
            JobPriority.Highest
        );

        neddle.AddJob(async () => {
            await Task.Delay(100);
            Assert.True(neddle.IsRunning);
            await Task.Delay(100);
        }, JobPriority.Lowest);

        neddle.RunAsync().Wait();
    }

    [Fact]
    public void IsRunning_WhenFalseSinceNotStartedYet()
    {
        Assert.False(Pincushion.Instance.GetNeedle().IsRunning);
    }

    [Fact]
    public void IsRunning_WhenFalseSinceWorkIsAlreadyDone()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);

        neddle.AddJob(() => Console.WriteLine($"Just testing {nameof(IsRunning_WhenFalseSinceWorkIsAlreadyDone)}"));

        neddle.RunAsync().Wait();

        Assert.False(neddle.IsRunning);
    }

    [Fact]
    public void Completed()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);

        neddle.Completed += (object? sender, EventArgs e) => Assert.True(true);

        neddle.AddJob(() => Console.WriteLine($"Just testing {nameof(Completed)}"));
        neddle.RunAsync().Wait();
    }

    [Fact]
    public void JobFaulted()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);
        bool faulted = false;

        neddle.JobFaulted += (object? sender, Exception ex) => { faulted = true; };
        neddle.AddJob(() => throw new NotImplementedException());

        Assert.Throws<AggregateException>(() => neddle.RunAsync().Wait());
        Assert.True(faulted);
    }

    [Fact]
    public void Canceled()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);
        bool canceled = false;

        neddle.Canceled += (object? sender, EventArgs e) => { canceled = true; };
        neddle.AddJob(neddle.RequestCancellation, JobPriority.Highest);
        neddle.AddJob(() => { canceled = false; }, JobPriority.Lowest);
        neddle.RunAsync().Wait();

        Assert.True(canceled);
    }

    [Fact]
    public void BeginRung()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);
        bool completed = false;

        neddle.Completed += (object? sender, EventArgs e) => { completed = true; };
        neddle.AddJob(() => Console.WriteLine($"Just testing {nameof(BeginRung)}"));
        neddle.BeginRun();
        Task.Delay(100).Wait();

        Assert.True(completed);
    }

    [Fact]
    public void IntendedUse()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle();

        //Ten jobs (total delay is 100 if one thread only)
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));

        Stopwatch stopwatch = Stopwatch.StartNew();
        neddle.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
    }

    [Fact]
    public void TwoBatchesOfJobs()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle();

        //Ten jobs (total delay is 100 if one thread only)
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));

        Stopwatch stopwatch = Stopwatch.StartNew();
        neddle.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);

        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));
        neddle.AddJob(async () => await Task.Delay(10));

        stopwatch.Restart();
        neddle.RunAsync().Wait();
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 100);
    }

    [Fact]
    public void NotifyPropertyChanged_Test()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle(1);

        neddle.AddJob(() => Console.WriteLine($"Just testing {nameof(NotifyPropertyChanged_Test)}"));
        neddle.AddJob(() => Console.WriteLine("Ignore this. Nothing to see"));
        neddle.AddJob(() => Console.WriteLine("Told you. This is only testing"));
        neddle.AddJob(() => Console.WriteLine("Ok, nevermind. Do what you want"));

        neddle.PropertyChanged += (object? sender, PropertyChangedEventArgs e) =>
        {
            if (e is null || string.IsNullOrEmpty(e.PropertyName))
                return;

            if (e.PropertyName == nameof(INeddleWorker.Progress))
            {
                Assert.True(
                    neddle.Progress == 0
                    || neddle.Progress == 25
                    || neddle.Progress == 50
                    || neddle.Progress == 75
                    || neddle.Progress == 100
                );
            }

            if (e.PropertyName == nameof(INeddleWorker.CompletedJobsCount))
            {
                Assert.True(
                    neddle.CompletedJobsCount == 0
                    || neddle.CompletedJobsCount == 1
                    || neddle.CompletedJobsCount == 2
                    || neddle.CompletedJobsCount == 3
                    || neddle.CompletedJobsCount == 4
                );
            }
        };

        neddle.RunAsync().Wait();
    }

    [Fact]
    public void DisposeTest()
    {
        INeddleWorker neddle = Pincushion.Instance.GetNeedle();
        neddle.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { int completed = neddle.CompletedJobsCount; });
    }

    //AddJob (both), RequestCancellation() and RunAsyn/( are tested in previous test methods.
}
