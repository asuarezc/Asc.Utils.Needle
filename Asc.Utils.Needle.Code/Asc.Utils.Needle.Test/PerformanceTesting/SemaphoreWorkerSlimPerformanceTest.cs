namespace Asc.Utils.Needle.Test.PerformanceTesting;

public class SemaphoreWorkerSlimPerformanceTest
{
    [Fact]
    public async Task A_OneThread()
    {
        await RunSemaphoreAsync(1);
    }

    [Fact]
    public async Task B_TwoThreads()
    {
        await RunSemaphoreAsync(2);
    }

    [Fact]
    public async Task C_ThreeThreads()
    {
        await RunSemaphoreAsync(3);
    }

    [Fact]
    public async Task D_FourThreads()
    {
        await RunSemaphoreAsync(4);
    }

    [Fact]
    public async Task E_FiveThreads()
    {
        await RunSemaphoreAsync(5);
    }

    [Fact]
    public async Task F_SixThreads()
    {
        await RunSemaphoreAsync(6);
    }

    [Fact]
    public async Task G_SevenThreads()
    {
        await RunSemaphoreAsync(7);
    }

    [Fact]
    public async Task H_EightThreads()
    {
        await RunSemaphoreAsync(8);
    }

    [Fact]
    public async Task I_NineThreads()
    {
        await RunSemaphoreAsync(9);
    }

    [Fact]
    public async Task J_TenThreads()
    {
        await RunSemaphoreAsync(10);
    }

    [Fact]
    public async Task K_ElevenThreads()
    {
        await RunSemaphoreAsync(11);
    }

    [Fact]
    public async Task L_TwelveThreads()
    {
        await RunSemaphoreAsync(12);
    }

    [Fact]
    public async Task M_ThirteenThreads()
    {
        await RunSemaphoreAsync(13);
    }

    [Fact]
    public async Task N_FourteenThreads()
    {
        await RunSemaphoreAsync(14);
    }

    [Fact]
    public async Task O_FifteenThreads()
    {
        await RunSemaphoreAsync(15);
    }

    [Fact]
    public async Task P_SixteenThreads()
    {
        await RunSemaphoreAsync(16);
    }

    [Fact]
    public async Task Q_SeventeenThreads()
    {
        await RunSemaphoreAsync(17);
    }

    [Fact]
    public async Task R_EighteenThreads()
    {
        await RunSemaphoreAsync(18);
    }

    [Fact]
    public async Task S_NineteenThreads()
    {
        await RunSemaphoreAsync(19);
    }

    [Fact]
    public async Task T_TwentyThreads()
    {
        await RunSemaphoreAsync(20);
    }

    [Fact]
    public async Task U_TwentyOneThreads()
    {
        await RunSemaphoreAsync(21);
    }

    [Fact]
    public async Task V_TwentyTwoThreads()
    {
        await RunSemaphoreAsync(22);
    }

    [Fact]
    public async Task W_TwentyThreeThreads()
    {
        await RunSemaphoreAsync(23);
    }

    [Fact]
    public async Task X_TwentyFourThreads()
    {
        await RunSemaphoreAsync(24);
    }

    [Fact]
    public async Task Y_TwentyFiveThreads()
    {
        await RunSemaphoreAsync(25);
    }

    [Fact]
    public void Z_Control()
    {
        for (int i = 0; i < 100; i++)
            GetJob();
    }

    private async Task RunSemaphoreAsync(int numberOfThreads)
    {
        using INeedleWorkerSlim worker = Pincushion.Instance.GetSemaphoreWorkerSlim(numberOfThreads);

        AddOneHundredJobsTo(worker);
        await worker.RunAsync();
    }

    private void AddOneHundredJobsTo(INeedleWorkerSlim worker)
    {
        for (int i = 0; i < 100; i++)
            worker.AddJob(GetJob);
    }

    private void GetJob()
    {
        Task.Delay(10).Wait();
    }
}
