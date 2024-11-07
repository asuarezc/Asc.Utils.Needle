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

        Pincushion.Instance.MasterNeddle.JobCompleted += (object? sender, EventArgs e) =>
        {
            if (!stopwatch.IsRunning)
                return;

            completedJobs++;

            if (completedJobs == 10 && stopwatch.IsRunning)
                stopwatch.Stop();
        };

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
    }

    [Fact]
    public void WhenAddingNullJob()
    {
#pragma warning disable CS8625 // Cannot conver a NULL literal in a reference type that does not accept NULL values.
        Assert.ThrowsAsync<ArgumentNullException>(async () => await Pincushion.Instance.MasterNeddle.AddJobAsync(null));
#pragma warning restore CS8625 // Cannot conver a NULL literal in a reference type that does not accept NULL values.
    }
}
