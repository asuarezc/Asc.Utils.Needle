using System.Diagnostics;

namespace Asc.Utils.Needle.Test.PerformanceTesting;

public class SemaphoreWorkerPerformanceTest
{
    private static readonly int TASK_DELAY_DURATION_MILLISECONDS = 10;
    private static readonly int NUMBER_OF_JOBS = 500;

    [Fact]
    public async Task A_OneThread()
    {
        await DoSemaphoreJobsAsync(1);
    }

    [Fact]
    public async Task B_TwoThreads()
    {
        await DoSemaphoreJobsAsync(2);
    }

    [Fact]
    public async Task C_ThreeThreads()
    {
        await DoSemaphoreJobsAsync(3);
    }

    [Fact]
    public async Task D_FourThreads()
    {
        await DoSemaphoreJobsAsync(4);
    }

    [Fact]
    public async Task E_FiveThreads()
    {
        await DoSemaphoreJobsAsync(5);
    }

    [Fact]
    public async Task F_SixThreads()
    {
        await DoSemaphoreJobsAsync(6);
    }

    [Fact]
    public async Task G_SevenThreads()
    {
        await DoSemaphoreJobsAsync(7);
    }

    [Fact]
    public async Task H_EightThreads()
    {
        await DoSemaphoreJobsAsync(8);
    }

    [Fact]
    public async Task I_NineThreads()
    {
        await DoSemaphoreJobsAsync(9);
    }

    [Fact]
    public async Task J_TenThreads()
    {
        await DoSemaphoreJobsAsync(10);
    }

    [Fact]
    public async Task K_ElevenThreads()
    {
        await DoSemaphoreJobsAsync(11);
    }

    [Fact]
    public async Task L_TwelveThreads()
    {
        await DoSemaphoreJobsAsync(12);
    }

    [Fact]
    public async Task M_ThirdteenThreads()
    {
        await DoSemaphoreJobsAsync(13);
    }

    [Fact]
    public async Task N_FourteenThreads()
    {
        await DoSemaphoreJobsAsync(14);
    }

    [Fact]
    public async Task O_FiveteenThreads()
    {
        await DoSemaphoreJobsAsync(15);
    }

    [Fact]
    public async Task P_SixteenThreads()
    {
        await DoSemaphoreJobsAsync(16);
    }

    [Fact]
    public async Task Q_SeventeenThreads()
    {
        await DoSemaphoreJobsAsync(17);
    }

    [Fact]
    public async Task R_EighteenThreads()
    {
        await DoSemaphoreJobsAsync(18);
    }

    [Fact]
    public async Task S_NineteenThreads()
    {
        await DoSemaphoreJobsAsync(19);
    }

    [Fact]
    public async Task T_TwentyThreads()
    {
        await DoSemaphoreJobsAsync(20);
    }

    [Fact]
    public async Task U_DefaultThreadLimit()
    {
        await DoSemaphoreJobsAsync();
    }

    [Fact]
    public async Task V_Control()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < NUMBER_OF_JOBS; i++)
            await Task.Delay(TimeSpan.FromMilliseconds(TASK_DELAY_DURATION_MILLISECONDS));

        stopwatch.Stop();

        Console.WriteLine($"Control: {stopwatch.ElapsedMilliseconds} ms");
    }

    private async Task DoSemaphoreJobsAsync()
    {
        List<Action> jobs = [];

        for (int i = 0; i < NUMBER_OF_JOBS; i++)
            jobs.Add(() => Task.Delay(TASK_DELAY_DURATION_MILLISECONDS).Wait());

        using ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker();

        foreach (Action job in jobs)
            worker.AddJob(job);

        await worker.RunAsync();
    }

    private async Task DoSemaphoreJobsAsync(int maxThreads)
    {
        List<Action> jobs = [];

        for (int i = 0; i < NUMBER_OF_JOBS; i++)
            jobs.Add(() => Task.Delay(TASK_DELAY_DURATION_MILLISECONDS).Wait());

        using ISemaphoreWorker worker = Needle.Instance.GetSemaphoreWorker(maxThreads);

        foreach (Action job in jobs)
            worker.AddJob(job);

        await worker.RunAsync();
    }
}
