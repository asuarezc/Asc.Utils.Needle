using System.Collections.Concurrent;
using System.ComponentModel;

namespace Asc.Utils.Needle.Test.UnitTesting;

public class SemaphoreWorkerTest
{
    #region SemaphoreWorkerSlimTests

    [Fact]
    public void AddSynchronousJob_IntendedUse()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();
        worker.AddJob(() => Console.WriteLine("Ignore this!"));
    }

    [Fact]
    public void AddSynchronousJob_WhenJobIsNull()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        Action? job = null;

#pragma warning disable CS8604 // Null reference argument
        Assert.Throws<ArgumentNullException>(() => worker.AddJob(job));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task AddSynchronousJob_WhenWorkingIsRunning()
    {
        bool running = false;
        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(async () =>
        {
            running = true;
            await Task.Delay(TimeSpan.FromSeconds(10));
            worker.Dispose();
            running = false;
        });

        //This is a bad practice. RunAsync must be always waited.
#pragma warning disable CS4014
        Task.Run(worker.RunAsync);
#pragma warning restore CS4014

        while (!running)
            await Task.Delay(TimeSpan.FromSeconds(0.1));

        Assert.Throws<InvalidOperationException>(() => worker.AddJob(() => Console.WriteLine("Ignore this!")));
    }

    [Fact]
    public void AddAsynchronousJob_IntendedUse()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();
        worker.AddJob(async () => await Task.Delay(100));
    }

    [Fact]
    public void AddAsynchronousJob_WhenJobIsNull()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        Func<Task>? job = null;

#pragma warning disable CS8604 // Null reference argument
        Assert.Throws<ArgumentNullException>(() => worker.AddJob(job));
#pragma warning restore CS8604
    }

    [Fact]
    public async Task AddAsynchronousJob_WhenWorkingIsRunning()
    {
        bool running = false;
        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(async () =>
        {
            running = true;
            await Task.Delay(TimeSpan.FromSeconds(10));
            worker.Dispose();
            running = false;
        });

        //This is a bad practice. RunAsync must be always waited.
#pragma warning disable CS4014
        Task.Run(worker.RunAsync);
#pragma warning restore CS4014

        while (!running)
            await Task.Delay(TimeSpan.FromSeconds(0.1));

        Assert.Throws<InvalidOperationException>(() => worker.AddJob(async () => await Task.Delay(100)));
    }

    [Fact]
    public async Task RunAsync_IntendedUse()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(() => Console.WriteLine("Ignore this!"));
        await worker.RunAsync();
    }

    [Fact]
    public async Task RunAsync_WhenThereIsNothingToRun()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();
        await Assert.ThrowsAsync<InvalidOperationException>(worker.RunAsync);
    }

    [Fact]
    public async Task RunAsync_WhenWorkerIsRunning()
    {
        bool running = false;
        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(async () =>
        {
            running = true;
            await Task.Delay(TimeSpan.FromSeconds(10));
            worker.Dispose();
            running = false;
        });

        //This is a bad practice. RunAsync must be always waited.
#pragma warning disable CS4014
        Task.Run(worker.RunAsync);
#pragma warning restore CS4014

        while (!running)
            await Task.Delay(TimeSpan.FromSeconds(0.1));

        await Assert.ThrowsAsync<InvalidOperationException>(worker.RunAsync);
    }

    [Fact]
    public void Cancel_WhenWorkerIsNotRunning()
    {
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();
        Assert.Throws<InvalidOperationException>(worker.Cancel);
    }

    [Fact]
    public async Task Cancel_WhenCancellationHasBeenAlreadyRequested()
    {
        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(() =>
        {
            worker.Cancel();
            worker.Cancel();
        });

        try
        {
            await worker.RunAsync();
            Assert.True(false);
        }
        catch (AggregateException ex)
        {
            Assert.NotNull(ex);
            Assert.NotNull(ex.InnerExceptions);
            Assert.Single(ex.InnerExceptions);
            Assert.True(ex.InnerExceptions.First() is InvalidOperationException);
        }
        catch (Exception)
        {
            Assert.True(false);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task CancelPendingJobsIfAnyOtherFails_WhenTrue()
    {
        List<object> objects = [];

        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker(
            cancelPendingJobsIfAnyOtherFails: true
        );

        worker.AddJob(() =>
        {
            throw new InvalidOperationException();
        });

        worker.AddJob(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1)); //Wait until other job throws exception

            if (!worker.CancellationToken.IsCancellationRequested)
                objects.Add(new object());
        });

        await Assert.ThrowsAsync<AggregateException>(worker.RunAsync);
        Assert.Empty(objects);
    }

    [Fact]
    public async Task CancelPendingJobsIfAnyOtherFails_WhenFalse()
    {
        List<object> objects = [];

        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker(
            cancelPendingJobsIfAnyOtherFails: false
        );

        worker.AddJob(() =>
        {
            throw new InvalidOperationException();
        });

        worker.AddJob(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1)); //Wait until other job throws exception

            if (!worker.CancellationToken.IsCancellationRequested)
                objects.Add(new object());
        });

        await Assert.ThrowsAsync<AggregateException>(worker.RunAsync);
        Assert.NotEmpty(objects);
    }

    [Fact]
    public async Task CanceledEvent_IntendedUse()
    {
        bool wasCanceled = false;
        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        void OnCanceled(object? sender, EventArgs e)
        {
            wasCanceled = true;
            worker.Canceled -= OnCanceled;
        }

        worker.Canceled += OnCanceled;
        worker.AddJob(worker.Cancel);

        try
        {
            await worker.RunAsync();
        }
        finally
        {
            worker.Dispose();
        }

        Assert.True(wasCanceled);
    }

    [Fact]
    public async Task OneBatchOfJobs()
    {
        ConcurrentBag<object> bag = [];
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));

        await worker.RunAsync();

        Assert.Equal(5, bag.Count);
    }

    [Fact]
    public async Task TwoBatchsOfJobs()
    {
        ConcurrentBag<object> bag = [];
        using INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));

        await worker.RunAsync();
        Assert.Equal(5, bag.Count);

        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));

        await worker.RunAsync();
        Assert.Equal(10, bag.Count);
    }

    #endregion

    #region SemaphoreWorkerTests

    [Fact]
    public async Task JobFaultedEventAndSomeProperties()
    {
        int faulted = 0;
        ConcurrentBag<object> bag = [];

        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker(
            cancelPendingJobsIfAnyOtherFails: false
        );

        Assert.Equal(0, worker.FaultedJobsCount);
        Assert.Equal(0, worker.SuccessfullyCompletedJobsCount);
        Assert.Equal(0, worker.TotalJobsCount);

        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));

        void OnJobFaulted(object? sender, Exception ex)
        {
            faulted++;
            Assert.NotNull(ex);
            Assert.True(ex is InvalidOperationException);
        };

        worker.JobFaulted += OnJobFaulted;
        await Assert.ThrowsAsync<AggregateException>(worker.RunAsync);
        worker.JobFaulted -= OnJobFaulted;

        Assert.Equal(4, faulted);
        Assert.Equal(5, bag.Count);
        Assert.Equal(4, worker.FaultedJobsCount);
        Assert.Equal(5, worker.SuccessfullyCompletedJobsCount);
        Assert.Equal(9, worker.TotalJobsCount);

        worker.Dispose();
    }

    [Fact]
    public async Task IsRunningProperty()
    {
        bool running = false;
        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        worker.AddJob(async () =>
        {
            running = true;
            await Task.Delay(TimeSpan.FromSeconds(1));
            running = false;
        });

        //This is a bad practice. RunAsync must be always waited.
#pragma warning disable CS4014
        Task.Run(worker.RunAsync);
#pragma warning restore CS4014

        while (!running)
            await Task.Delay(TimeSpan.FromSeconds(0.1));

        Assert.True(worker.IsRunning);

        while (running)
            await Task.Delay(TimeSpan.FromSeconds(0.1));

        Assert.False(worker.IsRunning);
        worker.Dispose();
    }

    [Fact]
    public async Task PropertyChanged()
    {
        bool isRunningChecked = false;
        bool totalJobsCountChecked = false;
        bool successfullyCompletedJobsCountChecked = false;
        bool faultedJobsCountChecked = false;

        ConcurrentBag<object> bag = [];

        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.PropertyName))
                return;

            switch (e.PropertyName)
            {
                case nameof(INeedleWorker.IsRunning):
                    isRunningChecked = true;
                    break;

                case nameof(INeedleWorker.TotalJobsCount):
                    totalJobsCountChecked = true;
                    break;

                case nameof(INeedleWorker.SuccessfullyCompletedJobsCount):
                    successfullyCompletedJobsCountChecked = true;
                    break;

                case nameof(INeedleWorker.FaultedJobsCount):
                    faultedJobsCountChecked = true;
                    break;

                default: throw new InvalidOperationException();
            }
        }

        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker(
            cancelPendingJobsIfAnyOtherFails: false
        );

        worker.PropertyChanged += OnPropertyChanged;

        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => throw new InvalidOperationException());
        worker.AddJob(() => bag.Add(new object()));

        await Assert.ThrowsAsync<AggregateException>(worker.RunAsync);

        worker.PropertyChanged -= OnPropertyChanged;
        worker.Dispose();

        Assert.True(isRunningChecked);
        Assert.True(totalJobsCountChecked);
        Assert.True(successfullyCompletedJobsCountChecked);
        Assert.True(faultedJobsCountChecked);
    }

    #endregion
}
