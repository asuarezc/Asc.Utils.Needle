namespace Asc.Utils.Needle.Test.UnitTesting;

public class SemaphoreWorkerSlimTest
{
    #region AddJob method (Synchronous)

    [Fact]
    public void AddSynchronousJob_IntendedUse()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();
        worker.AddJob(() => Console.WriteLine("Ignore this!"));
    }

    [Fact]
    public void AddSynchronousJob_WhenJobIsNull()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

        Action? job = null;

#pragma warning disable CS8604 // Null reference argument
        Assert.Throws<ArgumentNullException>(() => worker.AddJob(job));
#pragma warning restore CS8604
    }

    [Fact]
    public void AddSynchronousJob_WhenWorkerIsRunning()
    {
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

        worker.AddJob(async () => await Task.Delay(TimeSpan.FromSeconds(1)));

        Task.Run(async () =>
        {
            await worker.RunAsync();
            worker.Dispose();
        });

#pragma warning disable IDE0079 // Remove unnecessary deletion
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        Task.Delay(TimeSpan.FromSeconds(0.25)).Wait(); // Need to wait until worker is running
#pragma warning restore xUnit1031
#pragma warning restore IDE0079

        Assert.Throws<InvalidOperationException>(() =>
            worker.AddJob(() => Console.WriteLine("Fail!"))
        );
    }

    #endregion

    #region AddJob method (Asynchronous)

    [Fact]
    public void AddAsynchronousJob_IntendedUse()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();
        worker.AddJob(async () => await Task.Delay(100));
    }

    [Fact]
    public void AddAsynchronousJob_WhenJobIsNull()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

        Func<Task>? job = null;

#pragma warning disable CS8604 // Null reference argument
        Assert.Throws<ArgumentNullException>(() => worker.AddJob(job));
#pragma warning restore CS8604
    }

    [Fact]
    public void AddAsynchronousJob_WhenWorkerIsRunning()
    {
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

        worker.AddJob(async () => await Task.Delay(TimeSpan.FromSeconds(1)));

        Task.Run(async () =>
        {
            await worker.RunAsync();
            worker.Dispose();
        });

#pragma warning disable IDE0079 // Remove unnecessary deletion
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
        Task.Delay(TimeSpan.FromSeconds(0.1)).Wait(); // Need to wait until worker is running
#pragma warning restore xUnit1031
#pragma warning restore IDE0079

        Assert.Throws<InvalidOperationException>(() =>
            worker.AddJob(async () => await Task.Delay(1))
        );
    }

    #endregion

    #region RunAsync method

    [Fact]
    public async Task RunAsync_IntendedUse()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

        worker.AddJob(() => Console.WriteLine("Ignore this!"));
        await worker.RunAsync();
    }

    [Fact]
    public async Task RunAsync_WhenThereIsNothingToRun()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();
        await Assert.ThrowsAsync<InvalidOperationException>(worker.RunAsync);
    }

    [Fact]
    public async Task RunAsync_WhenWorkerIsAlreadyRunning()
    {
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

        worker.AddJob(async () => await Task.Delay(TimeSpan.FromSeconds(1)));

#pragma warning disable CS4014 // Not awaited async invoke
        Task.Run(async () =>
        {
            await worker.RunAsync();
            worker.Dispose();
        });
#pragma warning restore CS4014

        await Task.Delay(TimeSpan.FromSeconds(0.1)); // Need to wait until worker is running
        await Assert.ThrowsAsync<InvalidOperationException>(worker.RunAsync);
    }

    #endregion

    #region Cancel method

    [Fact]
    public void Cancel_WhenWorkerIsNotRunning()
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();
        Assert.Throws<InvalidOperationException>(worker.Cancel);
    }

    [Fact]
    public async Task Cancel_WhenCancellationHasBeenAlreadyRequested()
    {
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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

    #endregion

    #region CancelPendingJobsIfAnyOtherFails and CancellationToken properties

    [Fact]
    public async Task CancelPendingJobsIfAnyOtherFails_WhenTrue()
    {
        List<object> objects = [];

        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim(
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

        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim(
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

    #endregion

    #region Canceled event

    [Fact]
    public async Task CanceledEvent_IntendedUse()
    {
        bool wasCanceled = false;
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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