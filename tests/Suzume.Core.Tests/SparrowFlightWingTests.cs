using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowFlightWingTests
{
    private const float Dt = 1f / 60f;
    private static readonly Vector3 Near = new(0.9f, 0f, -1.1f);
    // 飛んで向かうほど遠い目標
    private static readonly Vector3 Far = new(12f, 0f, 0f);

    private static SparrowInput Input(Vector3 target) =>
        new(Dt, target, 0f, target - Near, Vector3.Zero, false, target - Near + new Vector3(0f, 1.7f, 0f));

    private static List<SparrowPose> FlyToFarTarget()
    {
        var brain = new SparrowBrain(new SparrowParams { WanderDelay = float.PositiveInfinity }, new SeededRandom(1));
        brain.Tick(Input(Near));
        var poses = new List<SparrowPose>();
        for (var i = 0; i < 60 * 6; i++)
            poses.Add(brain.Tick(Input(Far)));
        return poses;
    }

    [Fact]
    public void BeatsItsWingsQuicklyWhileFlying()
    {
        var poses = FlyToFarTarget();

        var flying = poses.Where(x => x.State is SparrowState.TakeOff or SparrowState.Fly).ToList();
        Assert.True(flying.Count > 30);
        var signChanges = flying.Zip(flying.Skip(1)).Count(x => MathF.Sign(x.First.WingFlap) != MathF.Sign(x.Second.WingFlap) && x.Second.WingFlap != 0f);
        var perSecond = signChanges / (flying.Count * Dt);
        // 1 回の羽ばたきで 2 回符号が変わる。滑空で休む分を入れても毎秒 4 回以上は羽ばたく
        Assert.True(perSecond >= 8f, $"翼の上下の切り替えが毎秒 {perSecond:F1} 回");
        Assert.True(flying.Max(x => MathF.Abs(x.WingFlap)) >= Angles.DegToRad(25f),
            $"翼を振る角度が最大 {flying.Max(x => MathF.Abs(x.WingFlap)) * 180f / MathF.PI:F1}°");
    }

    [Fact]
    public void FoldsItsWingsDuringTheGlides()
    {
        var poses = FlyToFarTarget();

        var flying = poses.Where(x => x.State is SparrowState.Fly).ToList();
        Assert.Contains(flying, x => x.WingSpread < Angles.DegToRad(3f));
        Assert.Contains(flying, x => x.WingSpread > Angles.DegToRad(10f));
    }

    [Fact]
    public void FoldsItsWingsAfterLanding()
    {
        var poses = FlyToFarTarget();

        var landStart = poses.FindIndex(x => x.State == SparrowState.Land);
        Assert.True(landStart > 0);
        Assert.Contains(poses.Skip(landStart), x => x.State is SparrowState.Idle or SparrowState.Hop);
        Assert.Equal(0f, poses[^1].WingSpread);
        Assert.Equal(0f, poses[^1].WingFlap);
    }
}
