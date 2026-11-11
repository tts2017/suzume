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
        CraftQuickCircleIntervalMin = 5f,
        CraftQuickCircleIntervalMax = 5f,
    };

    private static SparrowInput Input(bool crafting = false, bool perch = false, bool completed = false, bool quick = false) =>
        new(Dt, Player + FollowSpot, 0f, Player, Vector3.Zero, false, Player + Head, perch, Crafting: crafting, CraftCompleted: completed,
            QuickSynthesis: quick);

    /// <summary>頭のまわりを回った角度 (ラジアン、向きは問わない)。</summary>
    private static float SweptAroundHead(IEnumerable<SparrowPose> poses)
    {
        var angles = poses.Where(x => x.State is SparrowState.Fly && Angles.HorizontalDistance(x.Position, Head) > 0.4f)
            .Select(x => MathF.Atan2(x.Position.X - Head.X, x.Position.Z - Head.Z)).ToList();
        return MathF.Abs(angles.Zip(angles.Skip(1), (a, b) => Angles.Wrap(b - a)).Sum());
    }

    /// <summary>制作中に、最初のフレームで制作が完成する。</summary>
    private static SparrowInput CompletedAtStart(int frame) => Input(crafting: true, completed: frame == 0);

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
    public void CirclesTheHeadTwiceWhenACraftIsCompleted()
    {
        var p = Params();
        var brain = CraftingBrain(p);

        var poses = Run(brain, CompletedAtStart, 10f);

        var flying = poses.Where(x => x.State is SparrowState.Fly).ToList();
        Assert.NotEmpty(flying);
        // 頭のそばを離れずに回る
        Assert.All(flying, x =>
        {
            var away = Angles.HorizontalDistance(x.Position, Head);
            Assert.True(away < p.CraftCircleRadius + 0.6f, $"頭から {away}");
            Assert.InRange(x.Position.Y, Head.Y - 0.05f, Head.Y + p.CraftCircleAltitudeMax + 0.6f);
        });
        // 頭のまわりを 2 周まわる (3 周目には入らない)
        var laps = SweptAroundHead(poses) / MathF.Tau;
        Assert.InRange(laps, 1.8f, 2.3f);
        // 回り終えたら頭に戻って待つ
        Assert.Equal(SparrowState.Perched, poses[^1].State);
        Assert.Equal(Head, poses[^1].Position);
    }

    [Fact]
    public void WaitsOnTheHeadUntilACraftIsCompleted()
    {
        var brain = CraftingBrain(Params());

        var poses = Run(brain, _ => Input(crafting: true), 40f);

        Assert.All(poses, x => Assert.Equal(SparrowState.Perched, x.State));
    }

    [Fact]
    public void CirclesOnceForEachCompletedCraft()
    {
        var brain = CraftingBrain(Params());

        // 0 秒と 15 秒に制作が完成する
        var poses = Run(brain, i => Input(crafting: true, completed: i == 0 || i == 15 * 60), 30f);

        Assert.Equal(2, poses.Count(x => x.Triggers.HasFlag(SparrowTrigger.TakeOff)));
        var returns = poses.Zip(poses.Skip(1)).Count(x => x.First.State != SparrowState.Perched && x.Second.State == SparrowState.Perched);
        Assert.Equal(2, returns);
        Assert.Equal(SparrowState.Perched, poses[^1].State);
    }

    [Fact]
    public void CirclesAtIntervalsDuringQuickSynthesis()
    {
        var brain = CraftingBrain(Params());

        var poses = Run(brain, _ => Input(crafting: true, quick: true), 30f);

        Assert.True(poses.Count(x => x.Triggers.HasFlag(SparrowTrigger.TakeOff)) >= 3);
        var returns = poses.Zip(poses.Skip(1)).Count(x => x.First.State != SparrowState.Perched && x.Second.State == SparrowState.Perched);
        Assert.True(returns >= 3, $"頭に戻ったのは {returns} 回");
    }

    [Fact]
    public void QuickSynthesisWaitsARandomTimeBetweenCircles()
    {
        var p = Params();
        p.CraftQuickCircleIntervalMin = 4f;
        p.CraftQuickCircleIntervalMax = 12f;
        var brain = CraftingBrain(p);

        var poses = Run(brain, _ => Input(crafting: true, quick: true), 120f);

        // 頭の上で待った時間がばらつく
        var waits = new List<int>();
        var run = 0;
        foreach (var pose in poses)
        {
            if (pose.State == SparrowState.Perched)
                run++;
            else if (run > 0)
            {
                waits.Add(run);
                run = 0;
            }
        }
        Assert.True(waits.Count >= 4, $"待った回数 {waits.Count}");
        Assert.True(waits.Max() - waits.Min() > 60, $"待った時間 {string.Join(", ", waits)}");
    }

    [Fact]
    public void CirclesOnceItReachesTheHeadIfTheCraftWasCompletedOnTheWay()
    {
        var brain = new SparrowBrain(Params(), new SeededRandom(1));
        brain.Tick(Input());

        var poses = Run(brain, i => Input(crafting: true, completed: i == 1), 12f);

        // 頭へ向かう 1 回と、頭のまわりを回る 1 回
        Assert.Equal(2, poses.Count(x => x.Triggers.HasFlag(SparrowTrigger.TakeOff)));
        Assert.Equal(SparrowState.Perched, poses[^1].State);
    }

    [Fact]
    public void ForgetsACompletedCraftWhenCraftingEnds()
    {
        var brain = new SparrowBrain(Params(), new SeededRandom(1));
        brain.Tick(Input());
        Run(brain, i => Input(crafting: true, completed: i == 1), 0.2f);
        Run(brain, _ => Input(), 8f);

        // 次の制作では、完成するまで頭の上で待つ
        var poses = Run(brain, _ => Input(crafting: true), 20f);

        Assert.Equal(1, poses.Count(x => x.Triggers.HasFlag(SparrowTrigger.TakeOff)));
        Assert.Equal(SparrowState.Perched, poses[^1].State);
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
        Run(brain, CompletedAtStart, 1.5f);

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
        Run(brain, CompletedAtStart, 1.5f);

        brain.Tick(Input(crafting: true, perch: true));
        var poses = Run(brain, _ => Input(crafting: true), 8f);

        GroundAssert.OnGround(0f, poses[^1]);
    }
}
