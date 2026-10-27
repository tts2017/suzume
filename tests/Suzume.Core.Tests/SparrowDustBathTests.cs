using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowDustBathTests
{
    private const float Dt = 1f / 60f;
    private static readonly Vector3 Target = new(0.9f, 0f, -1.1f);
    private static readonly Vector3 Player = Vector3.Zero;

    // 動き回りを止め、止まったらすぐ砂浴びを始める設定
    private static SparrowParams AlwaysBathe() => new()
    {
        WanderDelay = float.PositiveInfinity,
        DustBathDelay = 0f,
        DustBathIntervalMin = 0f, DustBathIntervalMax = 0f,
    };

    private static SparrowInput Input(Vector3? velocity = null, bool perch = false, Vector3? player = null)
    {
        var at = player ?? Player;
        return new(Dt, Target, 0f, at, velocity ?? Vector3.Zero, false, at + new Vector3(0f, 1.7f, 0f), perch);
    }

    private static List<SparrowPose> Run(SparrowBrain brain, SparrowInput input, float seconds)
    {
        var poses = new List<SparrowPose>();
        for (var i = 0; i < (int)MathF.Round(seconds / Dt); i++)
            poses.Add(brain.Tick(input));
        return poses;
    }

    private static SparrowBrain BathingBrain(SparrowParams p, out List<SparrowPose> lead)
    {
        var brain = new SparrowBrain(p, new SeededRandom(1));
        lead = Run(brain, Input(), 2f);
        Assert.Contains(lead, x => x.State == SparrowState.DustBath);
        return brain;
    }

    [Fact]
    public void DustBathesCrouchedWithFlutteringAndRolling()
    {
        var p = AlwaysBathe();
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, Input(), 12f);

        var bath = poses.Where(x => x.State == SparrowState.DustBath).ToList();
        Assert.NotEmpty(bath);
        Assert.Contains(bath, x => x.Anim == SparrowAnim.Flutter);
        Assert.Contains(bath, x => x.Anim == SparrowAnim.Idle); // 小休止
        Assert.Contains(bath, x => x.Position.Y < -0.01f); // 腹ばいに沈む
        Assert.Contains(bath, x => MathF.Abs(x.Roll) > Angles.DegToRad(5f));
        Assert.True(poses.Count(x => x.Triggers.HasFlag(SparrowTrigger.DustFlutter)) >= 2);
    }

    [Fact]
    public void ShakesOffAndReturnsToIdle()
    {
        var p = AlwaysBathe();
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, Input(), 12f);

        var bathEnd = poses.FindIndex(poses.FindIndex(x => x.State == SparrowState.DustBath), x => x.State != SparrowState.DustBath);
        Assert.True(bathEnd > 0);
        var before = poses.Take(bathEnd).Reverse().Take((int)(p.ShakeDuration / Dt)).ToList();
        // 最後は立った姿勢 (待機アニメ・沈みなし) で体を左右に振るう
        Assert.All(before, x => Assert.Equal(SparrowAnim.Idle, x.Anim));
        Assert.Contains(before, x => MathF.Abs(x.Roll) > Angles.DegToRad(10f));
        Assert.Equal(SparrowState.Idle, poses[bathEnd].State);
        Assert.Equal(0f, poses[bathEnd].Roll);
        Assert.Equal(0f, poses[bathEnd].Position.Y, 4);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void DustBathesWithinAMinuteOfStandingStillByDefault(int seed)
    {
        // 既定の設定 (動き回りあり) のまま、プレイヤーが止まっている
        var p = new SparrowParams();
        var brain = new SparrowBrain(p, new SeededRandom(seed));

        var poses = Run(brain, Input(), p.DustBathDelay + p.DustBathIntervalMax + 10f);

        Assert.Contains(poses, x => x.State == SparrowState.DustBath);
    }

    [Fact]
    public void DoesNotDustBatheWhilePlayerMoves()
    {
        var p = AlwaysBathe();
        p.DustBathDelay = 3f;
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, Input(velocity: new Vector3(0.5f, 0f, 0f)), 10f);

        Assert.DoesNotContain(poses, x => x.State == SparrowState.DustBath);
    }

    [Fact]
    public void AlarmInterruptsDustBath()
    {
        var brain = BathingBrain(AlwaysBathe(), out _);
        var bird = Target;
        var rush = Input(velocity: new Vector3(6f, 0f, 0f), player: bird - new Vector3(1.5f, 0f, 0f));

        var pose = brain.Tick(rush);

        Assert.Equal(SparrowState.Alarm, pose.State);
        Assert.Equal(0f, pose.Roll);
        Assert.True(pose.Position.Y >= 0f);
    }

    [Fact]
    public void BeckonDuringDustBathTakesOff()
    {
        var brain = BathingBrain(AlwaysBathe(), out _);

        var pose = brain.Tick(Input(perch: true));

        Assert.Equal(SparrowState.TakeOff, pose.State);
        Assert.Equal(0f, pose.Roll);
    }

    [Fact]
    public void StillNeverDustBathesWithDefaultChanceInShortIdle()
    {
        // 止まってすぐ (DustBathDelay 未満) には始めない
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = 0f, DustBathIntervalMax = 0f };
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, Input(), p.DustBathDelay - 0.5f);

        Assert.DoesNotContain(poses, x => x.State == SparrowState.DustBath);
    }
}
