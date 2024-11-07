using System.Diagnostics;

namespace Asc.Utils.Needle.Test;

public class MasterNeddleWorkerTest
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

        Pincushion.Instance.MasterNeddle.JobCompleted += onJobCompleted;

        stopwatch.Start();

        //Testing sincronous and asincronous jobs
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => { counter++; await Task.Delay(100); });
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => { counter++; Task.Delay(100).Wait(); });

        await Task.Delay(2000);

        Assert.True(stopwatch.ElapsedMilliseconds <= 1000);
        Assert.Equal(0, counter);

        Pincushion.Instance.MasterNeddle.JobCompleted -= onJobCompleted;
    }

    [Fact]
    public void WhenAddingNullJob()
    {
#pragma warning disable CS8625 // Cannot conver a NULL literal in a reference type that does not accept NULL values.
        Assert.ThrowsAsync<ArgumentNullException>(async () => await Pincushion.Instance.MasterNeddle.AddJobAsync(null));
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

        Pincushion.Instance.MasterNeddle.JobCompleted += onJobCompleted;
        Pincushion.Instance.MasterNeddle.JobFaulted += onJobFaulted;

        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => throw new InvalidOperationException());
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => throw new InvalidOperationException());
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => throw new InvalidOperationException());
        await Pincushion.Instance.MasterNeddle.AddJobAsync(async () => await Task.Delay(100));
        await Pincushion.Instance.MasterNeddle.AddJobAsync(() => throw new InvalidOperationException());

        await Task.Delay(1000);
        Assert.Equal(4, completedJobs);
        Assert.Equal(4, faultedJobs);

        Pincushion.Instance.MasterNeddle.JobCompleted -= onJobCompleted;
        Pincushion.Instance.MasterNeddle.JobFaulted -= onJobFaulted;
    }
}
