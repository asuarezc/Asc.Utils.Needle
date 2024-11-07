using System.Diagnostics;

namespace Asc.Utils.Needle.Test;

public class MasterNeedleWorkerTest
{
    [Fact]
    public async Task IntendedUse()
    {
        int completedJobs = 0;
        int counter = -10;
        Stopwatch stopwatch = new();

        void onJobCompleted(object? sender, EventArgs e)
        {
            if (!stopwatch.IsRunning)
                return;

            completedJobs++;

            if (completedJobs == 10 && stopwatch.IsRunning)
                stopwatch.Stop();
        };

        Pincushion.Instance.MasterNeedle.JobCompleted += onJobCompleted;

        stopwatch.Start();

        //Testing sincronous and asincronous jobs
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });

        await Task.Delay(2000);

        Assert.True(stopwatch.ElapsedMilliseconds <= 1000);
        Assert.Equal(0, counter);

        Pincushion.Instance.MasterNeedle.JobCompleted -= onJobCompleted;
    }

    [Fact]
    public void WhenAddingNullJob()
    {
#pragma warning disable CS8625 // Cannot conver a NULL literal in a reference type that does not accept NULL values.
        Assert.ThrowsAsync<ArgumentNullException>(async () => await Pincushion.Instance.MasterNeedle.AddJobAsync(null));
#pragma warning restore CS8625 // Cannot conver a NULL literal in a reference type that does not accept NULL values.
    }

    [Fact]
    public async Task WhenJobFails()
    {
        int completedJobs = 0;
        int faultedJobs = 0;

        void onJobCompleted(object? sender, EventArgs e)
        {
            completedJobs++;
        }

        void onJobFaulted(object? sender, Exception ex)
        {
            faultedJobs++;
            Assert.True(ex is InvalidOperationException);
        }

        Pincushion.Instance.MasterNeedle.JobCompleted += onJobCompleted;
        Pincushion.Instance.MasterNeedle.JobFaulted += onJobFaulted;

        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => throw new InvalidOperationException());
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => throw new InvalidOperationException());
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => throw new InvalidOperationException());
        await Pincushion.Instance.MasterNeedle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeedle.AddJobAsync(() => throw new InvalidOperationException());

        await Task.Delay(1000);
        Assert.Equal(4, completedJobs);
        Assert.Equal(4, faultedJobs);

        Pincushion.Instance.MasterNeedle.JobCompleted -= onJobCompleted;
        Pincushion.Instance.MasterNeedle.JobFaulted -= onJobFaulted;
    }
}
