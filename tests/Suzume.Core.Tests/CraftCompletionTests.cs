using Suzume.Core;

namespace Suzume.Core.Tests;

public class CraftCompletionTests
{
    [Fact]
    public void ReportsTheMomentProgressReachesTheMaximum()
    {
        var detector = new CraftCompletion();

        Assert.False(detector.Update(open: true, progress: 0, maxProgress: 300));
        Assert.False(detector.Update(open: true, progress: 150, maxProgress: 300));
        Assert.True(detector.Update(open: true, progress: 300, maxProgress: 300));
    }

    [Fact]
    public void ReportsEachCraftOnlyOnce()
    {
        var detector = new CraftCompletion();
        detector.Update(open: true, progress: 0, maxProgress: 300);
        detector.Update(open: true, progress: 300, maxProgress: 300);

        Assert.False(detector.Update(open: true, progress: 300, maxProgress: 300));
        Assert.False(detector.Update(open: false, progress: 0, maxProgress: 0));
    }

    [Fact]
    public void ReportsTheNextCraftAfterTheWindowReopens()
    {
        var detector = new CraftCompletion();
        detector.Update(open: true, progress: 300, maxProgress: 300);
        detector.Update(open: false, progress: 0, maxProgress: 0);

        detector.Update(open: true, progress: 0, maxProgress: 200);
        Assert.True(detector.Update(open: true, progress: 210, maxProgress: 200));
    }

    [Fact]
    public void IgnoresAWindowWhoseMaximumIsNotKnown()
    {
        var detector = new CraftCompletion();

        Assert.False(detector.Update(open: true, progress: 0, maxProgress: 0));
        Assert.False(detector.Update(open: false, progress: 300, maxProgress: 300));
    }

    [Fact]
    public void DoesNotReportAFailedCraft()
    {
        var detector = new CraftCompletion();
        detector.Update(open: true, progress: 0, maxProgress: 300);
        detector.Update(open: true, progress: 250, maxProgress: 300);

        // 耐久が尽きて失敗し、ウィンドウが閉じる
        Assert.False(detector.Update(open: false, progress: 0, maxProgress: 0));
    }
}
