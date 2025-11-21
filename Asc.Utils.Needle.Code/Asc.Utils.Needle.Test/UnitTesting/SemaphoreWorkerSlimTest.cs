using System.Collections.Concurrent;

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
    public async Task AddSynchronousJob_WhenWorkingIsRunning()
    {
        bool running = false;
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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
    public async Task AddAsynchronousJob_WhenWorkingIsRunning()
    {
        bool running = false;
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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
    public async Task RunAsync_WhenWorkerIsRunning()
    {
        bool running = false;
        INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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

    #region CancelPendingJobsIfAnyOtherFails and CancellationToken property

    [Fact]
    public void CancelPendingJobsIfAnyOtherFails()
    {
        using INeedleWorkerSlim workerCancelPendingJobs =
            Pincushion.Instance.GetSemaphoreWorkerSlim(maxThreads: 5, OnJobFailedBehaviour.CancelPendingJobs);

        using INeedleWorkerSlim workerContinueRunningPendingJobs =
            Pincushion.Instance.GetSemaphoreWorkerSlim(OnJobFailedBehaviour.ContinueRunningPendingJobs);

        Assert.Equal(OnJobFailedBehaviour.CancelPendingJobs, workerCancelPendingJobs.OnJobFailedBehaviour);
        Assert.Equal(OnJobFailedBehaviour.ContinueRunningPendingJobs, workerContinueRunningPendingJobs.OnJobFailedBehaviour);
    }

    [Fact]
    public async Task CancelPendingJobsIfAnyOtherFails_WhenTrue()
    {
        List<object> objects = [];

        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim(
            onJobFailedBehaviour: OnJobFailedBehaviour.CancelPendingJobs
        );

        worker.AddJob(() => throw new InvalidOperationException());

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
            onJobFailedBehaviour: OnJobFailedBehaviour.ContinueRunningPendingJobs
        );

        worker.AddJob(() => throw new InvalidOperationException());

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
        return;

        void OnCanceled(object? sender, EventArgs e)
        {
            wasCanceled = true;
            worker.Canceled -= OnCanceled;
        }
    }

    #endregion

    #region Intended uses

    [Fact]
    public async Task OneBatchOfJobs()
    {
        ConcurrentBag<object> bag = [];
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim();

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
}