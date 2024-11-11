namespace Asc.Utils.Needle.Test;

public class NeedleTest
{
    [Fact]
    public void GetNeedle_WhenMaxThreadsEqualsZero()
    {
        Assert.Throws<ArgumentException>(() => Needle.Instance.GetSemaphoreWorker(0));
    }

    [Fact]
    public void GetNeedle_WhenMaxThreadsIsLowerThanZero()
    {
        Assert.Throws<ArgumentException>(() => Needle.Instance.GetSemaphoreWorker(-1));
    }

    [Fact]
    public void GetNeedle_WhenDefaultParameters()
    {
        Assert.NotNull(Needle.Instance.GetSemaphoreWorker());
    }

    [Fact]
    public void GetNeedle_WhenValidMaxThreadsParamValue()
    {
        Assert.NotNull(Needle.Instance.GetSemaphoreWorker(5));
    }

    [Fact]
    public void GetNeedle_WhenYouCannotWantToCancelWhenAJobFails()
    {
        Assert.NotNull(Needle.Instance.GetSemaphoreWorker(cancelPendingJobsIfAnyOtherFails: false));
    }

    [Fact]
    public void GetNeedle_WhenYourWantToShowThatYouKnowWhatYouWant()
    {
        Assert.NotNull(Needle.Instance.GetSemaphoreWorker(maxThreads: 2, cancelPendingJobsIfAnyOtherFails: false));
    }

    [Fact]
    public void MasterNeedle_NotNull()
    {
        Assert.NotNull(Needle.Instance.MainBackgroundWorker);
    }
}