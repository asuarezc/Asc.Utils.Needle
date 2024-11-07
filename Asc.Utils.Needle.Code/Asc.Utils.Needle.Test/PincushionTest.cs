namespace Asc.Utils.Needle.Test;

public class PincushionTest
{
    [Fact]
    public void GetNeedle_WhenMaxThreadsEqualsZero()
    {
        Assert.Throws<ArgumentException>(() => Pincushion.Instance.GetNeedle(0));
    }

    [Fact]
    public void GetNeedle_WhenMaxThreadsIsLowerThanZero()
    {
        Assert.Throws<ArgumentException>(() => Pincushion.Instance.GetNeedle(-1));
    }

    [Fact]
    public void GetNeedle_WhenDefaultParameters()
    {
        Assert.NotNull(Pincushion.Instance.GetNeedle());
    }

    [Fact]
    public void GetNeedle_WhenValidMaxThreadsParamValue()
    {
        Assert.NotNull(Pincushion.Instance.GetNeedle(5));
    }

    [Fact]
    public void GetNeedle_WhenYouCannotWantToCancelWhenAJobFails()
    {
        Assert.NotNull(Pincushion.Instance.GetNeedle(cancelPendingJobsIfAnyOtherFails: false));
    }

    [Fact]
    public void GetNeedle_WhenYourWantToShowThatYouKnowWhatYouWant()
    {
        Assert.NotNull(Pincushion.Instance.GetNeedle(maxThreads: 2, cancelPendingJobsIfAnyOtherFails: false));
    }

    [Fact]
    public void MasterNeedle_NotNull()
    {
        Assert.NotNull(Pincushion.Instance.MasterNeedle);
    }
}