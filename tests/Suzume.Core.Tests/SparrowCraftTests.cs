using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowCraftTests
{
    private const float Dt = 1f / 60f;
    private static readonly Vector3 Player = Vector3.Zero;
    private static readonly Vector3 FollowSpot = new(0.9f, 0f, -1.1f);
    private static readonly Vector3 Head = new(0f, 1.7f, 0f);

    private static SparrowParams Params() => new()
    {
        WanderDelay = float.PositiveInfinity,
        DustBathIntervalMin = float.PositiveInfinity,
        DustBathIntervalMax = float.PositiveInfinity,
        CraftCircleIntervalMin = 5f,
        CraftCircleIntervalMax = 5f,
        CraftCircleDurationMin = 3f,
        CraftCircleDurationMax = 3f,
    };

    private static SparrowInput Input(bool crafting = false, bool perch = false) =>
        new(Dt, Player + FollowSpot, 0f, Player, Vector3.Zero, false, Player + Head, perch, Crafting: crafting);

    private static List<SparrowPose> Run(SparrowBrain brain, Func<int, SparrowInput> input, float seconds)
    {
        var poses = new List<SparrowPose>();
        var frames = (int)MathF.Round(seconds / Dt);
        for (var i = 0; i < frames; i++)
            poses.Add(brain.Tick(input(i)));
        return poses;
    }

    /// <summary>制作を始めて頭に乗りきったところ。</summary>
    private static SparrowBrain CraftingBrain(SparrowParams p)
    {
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());
        Run(brain, _ => Input(crafting: true), 4f);
        return brain;
    }

    [Fact]
    public void PerchesOnHeadWhenCraftingStarts()
    {
        var brain = new SparrowBrain(Params(), new SeededRandom(1));
        brain.Tick(Input());

        var poses = Run(brain, _ => Input(crafting: true), 4f);

        Assert.True(poses[0].Triggers.HasFlag(SparrowTrigger.TakeOff));
        Assert.Equal(SparrowState.Perched, poses[^1].State);
        Assert.Equal(Head, poses[^1].Position);
    }

    [Fact]
    public void FliesAroundTheHeadAndComesBack()
    {
        var p = Params();
        var brain = CraftingBrain(p);

        var poses = Run(brain, _ => Input(crafting: true), 10f);

        var flying = poses.Where(x => x.State is SparrowState.Fly).ToList();
        Assert.NotEmpty(flying);
        // 頭のそばを離れずに回る
        Assert.All(flying, x =>
        {
            var away = Angles.HorizontalDistance(x.Position, Head);
            Assert.True(away < p.CraftCircleRadius + 0.6f, $"頭から {away}");
            Assert.InRange(x.Position.Y, Head.Y - 0.05f, Head.Y + p.CraftCircleAltitudeMax + 0.6f);
        });
        // 頭のまわりを半周以上まわる
        var angles = flying.Where(x => Angles.HorizontalDistance(x.Position, Head) > 0.4f)
            .Select(x => MathF.Atan2(x.Position.X - Head.X, x.Position.Z - Head.Z)).ToList();
        var swept = angles.Zip(angles.Skip(1), (a, b) => Angles.Wrap(b - a)).Sum();
        Assert.True(MathF.Abs(swept) > MathF.PI, $"回った角度 {swept}");
        // 回り終えたら頭に戻って待つ
        Assert.Equal(SparrowState.Perched, poses[^1].State);
        Assert.Equal(Head, poses[^1].Position);
    }

    [Fact]
    public void KeepsCirclingAtIntervalsWhileCrafting()
    {
        var brain = CraftingBrain(Params());

        var poses = Run(brain, _ => Input(crafting: true), 40f);

        Assert.True(poses.Count(x => x.Triggers.HasFlag(SparrowTrigger.TakeOff)) >= 3);
        // 回るたびに頭へ戻る
        var returns = poses.Zip(poses.Skip(1)).Count(x => x.First.State != SparrowState.Perched && x.Second.State == SparrowState.Perched);
        Assert.True(returns >= 3, $"頭に戻ったのは {returns} 回");
    }

    [Fact]
    public void GetsOffTheHeadWhenCraftingEnds()
    {
        var brain = CraftingBrain(Params());

        var first = brain.Tick(Input());
        var poses = Run(brain, _ => Input(), 8f);

        Assert.Equal(SparrowState.TakeOff, first.State);
        GroundAssert.OnGround(0f, poses[^1]);
    }

    [Fact]
    public void GoesToTheGroundIfCraftingEndsWhileCircling()
    {
        var brain = CraftingBrain(Params());
        Run(brain, _ => Input(crafting: true), 6f);

        var poses = Run(brain, _ => Input(), 8f);

        Assert.NotEqual(SparrowState.Perched, poses[^1].State);
        GroundAssert.OnGround(0f, poses[^1]);
    }

    [Fact]
    public void StaysOnTheHeadAfterCraftingIfItWasAlreadyThere()
    {
        var brain = new SparrowBrain(Params(), new SeededRandom(1));
        brain.Tick(Input());
        brain.Tick(Input(perch: true));
        Run(brain, _ => Input(), 4f);

        Run(brain, _ => Input(crafting: true), 2f);
        var poses = Run(brain, _ => Input(), 2f);

        Assert.All(poses, x => Assert.Equal(SparrowState.Perched, x.State));
    }

    [Fact]
    public void BeckonWhileCirclingSendsItToTheGround()
    {
        var brain = CraftingBrain(Params());
        Run(brain, _ => Input(crafting: true), 6f);

        brain.Tick(Input(crafting: true, perch: true));
        var poses = Run(brain, _ => Input(crafting: true), 8f);

        GroundAssert.OnGround(0f, poses[^1]);
    }
}
