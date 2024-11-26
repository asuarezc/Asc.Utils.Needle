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
}
