namespace Asc.Utils.Needle.Test.PerformanceTesting;

public class ParallelWorkerPerformanceTest
{
    [Fact]
    public async Task A_TenJobs()
    {
        await RunParallelAsync(10);
    }

    [Fact]
    public async Task B_TwentyJobs()
    {
        await RunParallelAsync(20);
    }

    [Fact]
    public async Task C_ThirtyJobs()
    {
        await RunParallelAsync(30);
    }

    [Fact]
    public async Task D_FortyJobs()
    {
        await RunParallelAsync(40);
    }

    [Fact]
    public async Task E_FiftyJobs()
    {
        await RunParallelAsync(50);
    }

    [Fact]
    public async Task F_SixtyJobs()
    {
        await RunParallelAsync(60);
    }

    [Fact]
    public async Task G_SeventyJobs()
    {
        await RunParallelAsync(70);
    }

    [Fact]
    public async Task H_EightyJobs()
    {
        await RunParallelAsync(80);
    }

    [Fact]
    public async Task I_NinetyJobs()
    {
        await RunParallelAsync(90);
    }

    [Fact]
    public async Task J_OneHundredJobs()
    {
        await RunParallelAsync(100);
    }

    [Fact]
    public void Z_Control()
    {
        for (int i = 0; i < 100; i++)
            GetJob();
    }

    private async Task RunParallelAsync(int numberOfWorks)
    {
        using INeedleWorker worker = Pincushion.Instance.GetParallelWorker();

        AddJobs(worker, numberOfWorks);
        await worker.RunAsync();
    }

    private void AddJobs(INeedleWorker worker, int numberOfWorks)
    {
        for (int i = 0; i < numberOfWorks; i++)
            worker.AddJob(GetJob);
    }

    private void GetJob()
    {
        Task.Delay(10).Wait();
    }
}
