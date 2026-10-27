using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowWanderPerchTests
{
    private const float Dt = 1f / 60f;
    private static readonly Vector3 Player = Vector3.Zero;
    private static readonly Vector3 FollowSpot = new(0.9f, 0f, -1.1f);
    private static readonly Vector3 Head = new(0f, 1.7f, 0f);

    private static SparrowInput Input(
        Vector3? player = null,
        Vector3? velocity = null,
        bool perch = false,
        bool jumping = false,
        bool mounted = false)
    {
        var at = player ?? Player;
        return new(Dt, at + FollowSpot, 0f, at, velocity ?? Vector3.Zero, mounted, at + Head, perch, jumping);
    }

    private static List<SparrowPose> Run(SparrowBrain brain, Func<int, SparrowInput> input, float seconds)
    {
        var poses = new List<SparrowPose>();
        var frames = (int)MathF.Round(seconds / Dt);
        for (var i = 0; i < frames; i++)
            poses.Add(brain.Tick(input(i)));
        return poses;
    }

    private static SparrowBrain PerchedBrain(SparrowParams p)
    {
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());
        brain.Tick(Input(perch: true));
        Run(brain, _ => Input(), 8f);
        return brain;
    }

    [Fact]
    public void WandersAroundStillPlayerByHopping()
    {
        var p = new SparrowParams();
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, _ => Input(), 40f);

        Assert.All(poses, x => Assert.Equal(SparrowAnim.Idle, x.Anim));
        Assert.All(poses, x =>
            Assert.True(Angles.HorizontalDistance(x.Position, Player) <= p.WanderRadiusMax + p.HopDistanceMax,
                $"プレイヤーから {Angles.HorizontalDistance(x.Position, Player)}"));
        // 何か所かを渡り歩く(最初の場所から離れたことがある)
        Assert.True(poses.Max(x => Vector3.Distance(x.Position, FollowSpot)) > 0.8f);
    }

    [Fact]
    public void PerchesOnHeadWhenBeckoned()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity };
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());

        var first = brain.Tick(Input(perch: true));
        var poses = Run(brain, _ => Input(), 8f);

        Assert.True(first.Triggers.HasFlag(SparrowTrigger.TakeOff));
        var final = poses[^1];
        Assert.Equal(SparrowState.Perched, final.State);
        Assert.Equal(SparrowAnim.Idle, final.Anim);
        Assert.Equal(Head, final.Position);
    }

    [Fact]
    public void StaysPerchedWhileWalking()
    {
        var brain = PerchedBrain(new SparrowParams { WanderDelay = float.PositiveInfinity });
        var walk = new Vector3(2f, 0f, 0f);

        var poses = Run(brain, i => Input(player: walk * (i * Dt), velocity: walk), 2f);

        Assert.All(poses.Select((pose, i) => (pose, i)), x =>
        {
            Assert.Equal(SparrowState.Perched, x.pose.State);
            Assert.Equal(walk * (x.i * Dt) + Head, x.pose.Position);
        });
    }

    [Theory]
    [InlineData("run")]
    [InlineData("jump")]
    [InlineData("mount")]
    [InlineData("beckon")]
    public void LeavesHead(string reason)
    {
        var brain = PerchedBrain(new SparrowParams { WanderDelay = float.PositiveInfinity });

        var pose = brain.Tick(reason switch
        {
            "run" => Input(velocity: new Vector3(6f, 0f, 0f)),
            "jump" => Input(jumping: true),
            "mount" => Input(mounted: true),
            _ => Input(perch: true),
        });

        Assert.Equal(SparrowState.TakeOff, pose.State);
        Assert.True(pose.Triggers.HasFlag(SparrowTrigger.TakeOff));
    }

    [Fact]
    public void ReturnsToGroundAfterLeavingHead()
    {
        var brain = PerchedBrain(new SparrowParams { WanderDelay = float.PositiveInfinity });
        brain.Tick(Input(perch: true));

        var poses = Run(brain, _ => Input(), 8f);

        Assert.Equal(SparrowAnim.Idle, poses[^1].Anim);
        Assert.Equal(0f, poses[^1].Position.Y, 3);
    }
}
