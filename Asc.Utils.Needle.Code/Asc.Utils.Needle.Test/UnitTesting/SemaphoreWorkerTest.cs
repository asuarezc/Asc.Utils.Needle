using System.Collections.Concurrent;

namespace Asc.Utils.Needle.Test.UnitTesting;

public class SemaphoreWorkerTest
{
    #region Base implementation

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

    #endregion

    [Fact]
    public async Task BeginRunAndCompletedEvent()
    {
        ConcurrentBag<object> bag = [];
        bool completedExecuted = false;

        INeedleWorker worker = Pincushion.Instance.GetSemaphoreWorker();

        void OnCompleted(object? sender, EventArgs e)
        {
            Assert.Equal(5, bag.Count);

            worker.Completed -= OnCompleted;
            worker.Dispose();
            completedExecuted = true;
        };

        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));
        worker.AddJob(() => bag.Add(new object()));

        worker.Completed += OnCompleted;
        worker.BeginRun();

        while (!completedExecuted)
            await Task.Delay(TimeSpan.FromSeconds(0.1));

        Assert.Throws<ObjectDisposedException>(() => Console.WriteLine(worker.ToString()));
    }

    [Fact]
    public async Task JobFaultedEvent()
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

}
