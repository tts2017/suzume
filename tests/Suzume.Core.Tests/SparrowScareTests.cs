using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowScareTests
{
    private const float Dt = 1f / 60f;
    // スズメの目標地点は原点、プレイヤーはその 2y 西 (-X) で止まっている
    private static readonly Vector3 Target = Vector3.Zero;
    private static readonly Vector3 Player = new(-2f, 0f, 0f);

    private static SparrowParams Calm() => new()
    {
        WanderDelay = float.PositiveInfinity,
        DustBathIntervalMin = float.PositiveInfinity,
        DustBathIntervalMax = float.PositiveInfinity,
    };

    private static SparrowInput Input(bool threatened = false, bool perch = false) =>
        new(Dt, Target, MathF.PI / 2f, Player, Vector3.Zero, false, Player + new Vector3(0f, 1.7f, 0f), perch, false, threatened);

    private static List<SparrowPose> Run(SparrowBrain brain, float seconds)
    {
        var poses = new List<SparrowPose>();
        for (var i = 0; i < (int)MathF.Round(seconds / Dt); i++)
            poses.Add(brain.Tick(Input()));
        return poses;
    }

    private static SparrowBrain ScaredBrain(SparrowParams p, out SparrowPose first)
    {
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());
        first = brain.Tick(Input(threatened: true));
        return brain;
    }

    [Fact]
    public void FliesAwayWithAlarmWhenThreatened()
    {
        var p = Calm();
        var brain = ScaredBrain(p, out var first);

        var poses = Run(brain, 4f);

        Assert.Equal(SparrowState.TakeOff, first.State);
        Assert.True(first.Triggers.HasFlag(SparrowTrigger.Alarm));
        var landed = poses[^1];
        Assert.Equal(SparrowAnim.Idle, landed.Anim);
        Assert.Equal(0f, landed.Position.Y, 3);
        // プレイヤーと反対 (+X) へ 4y 以上離れる
        Assert.True(landed.Position.X - Player.X >= 2f + p.ScareFleeDistanceMin - p.ArriveDistance - 0.1f,
            $"x={landed.Position.X}");
    }

    [Fact]
    public void KeepsDistanceDuringWaryPeriod()
    {
        var p = Calm();
        var brain = ScaredBrain(p, out _);
        // 逃げた先に降りるまで進める
        while (brain.Tick(Input()).State is SparrowState.TakeOff or SparrowState.Fly or SparrowState.Land)
        {
        }

        var wary = Run(brain, p.WaryDurationMin - 0.5f);

        Assert.All(wary, x => Assert.True(Angles.HorizontalDistance(x.Position, Target) >= 3f,
            $"目標から {Angles.HorizontalDistance(x.Position, Target)}"));
    }

    [Fact]
    public void ComesBackAfterWaryPeriod()
    {
        var p = Calm();
        var brain = ScaredBrain(p, out _);

        var poses = Run(brain, 4f + p.WaryDurationMax + 8f);

        Assert.True(Angles.HorizontalDistance(poses[^1].Position, Target) <= p.ArriveDistance + p.HopDistanceMax);
    }

    [Fact]
    public void FliesOffHeadWhenThreatened()
    {
        var p = Calm();
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());
        brain.Tick(Input(perch: true));
        var perched = Run(brain, 8f);
        Assert.Equal(SparrowState.Perched, perched[^1].State);

        var first = brain.Tick(Input(threatened: true));
        var poses = Run(brain, 5f);

        Assert.Equal(SparrowState.TakeOff, first.State);
        Assert.True(first.Triggers.HasFlag(SparrowTrigger.Alarm));
        Assert.Equal(0f, poses[^1].Position.Y, 3);
        Assert.True(Angles.HorizontalDistance(poses[^1].Position, Player) >= p.ScareFleeDistanceMin - p.ArriveDistance - 0.1f);
    }
}
