using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowBrainTests
{
    private const float Dt = 1f / 60f;

    private static SparrowBrain NewBrain(SparrowParams? parameters = null, int seed = 1) =>
        new(parameters ?? new SparrowParams(), new SeededRandom(seed));

    // プレイヤーは既定で論理位置の 3y 後ろに静止している(警戒の範囲外)
    private static SparrowInput Input(
        Vector3 target,
        Vector3? playerPosition = null,
        Vector3? playerVelocity = null,
        bool mounted = false) =>
        new(Dt, target, 0f, playerPosition ?? target + new Vector3(0f, 0f, -3f), playerVelocity ?? Vector3.Zero, mounted);

    private static List<SparrowPose> Run(SparrowBrain brain, SparrowInput input, float seconds)
    {
        var poses = new List<SparrowPose>();
        var frames = (int)MathF.Round(seconds / input.Dt);
        for (var i = 0; i < frames; i++)
            poses.Add(brain.Tick(input));
        return poses;
    }

    [Fact]
    public void FirstTickStartsAtTargetInIdle()
    {
        var target = new Vector3(10f, 2f, -5f);
        var pose = NewBrain().Tick(Input(target));
        Assert.Equal(target, pose.Position);
        Assert.Equal(SparrowState.Idle, pose.State);
        Assert.Equal(SparrowAnim.Idle, pose.Anim);
    }

    [Fact]
    public void MovesToNearbyTargetByHoppingWithPauses()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(2f, 0f, 0f);

        var poses = Run(brain, Input(target), 15f);

        Assert.Contains(poses, x => x.State == SparrowState.Hop);
        Assert.DoesNotContain(poses, x => x.Anim == SparrowAnim.Fly);
        Assert.True(poses.Max(x => x.Position.Y) > 0.05f, "ホップで体が浮くこと");
        Assert.True(Vector3.Distance(poses[^1].Position with { Y = 0f }, target) <= p.ArriveDistance + p.HopDistanceMax);
        // 滑るように動かず、移動の途中で止まっているフレームがある
        var stillFramesWhileFar = poses.Zip(poses.Skip(1)).Count(pair =>
            pair.First.Position == pair.Second.Position
            && Vector3.Distance(pair.First.Position, target) > p.ArriveDistance + 0.5f);
        Assert.True(stillFramesWhileFar > 10);
    }

    [Fact]
    public void YawNeverChangesMidHop()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));

        var poses = Run(brain, Input(new Vector3(0f, 0f, 3f)), 10f);

        foreach (var (prev, next) in poses.Zip(poses.Skip(1)))
        {
            var midAir = prev.Position.Y > 1e-4f && next.Position.Y > 1e-4f;
            if (midAir)
                Assert.Equal(prev.Yaw, next.Yaw);
        }
    }

    [Fact]
    public void LooksAroundWithInstantTurnsWhileIdle()
    {
        var p = new SparrowParams { ForageChance = 0f, PreenChance = 0f };
        var brain = NewBrain(p);

        var poses = Run(brain, Input(Vector3.Zero), 5f);

        var minSnap = Angles.DegToRad(p.ScanAngleMinDeg) - 1e-3f;
        var snaps = poses.Zip(poses.Skip(1)).Count(pair =>
            MathF.Abs(Angles.Wrap(pair.Second.Yaw - pair.First.Yaw)) >= minSnap);
        Assert.True(snaps >= 3, $"snaps={snaps}");
        Assert.All(poses, x => Assert.Equal(Vector3.Zero, x.Position));
    }

    [Fact]
    public void ForagingPecksDownAndRecovers()
    {
        var p = new SparrowParams { ForageChance = 1f };
        var brain = NewBrain(p);

        var poses = Run(brain, Input(Vector3.Zero), 4f);

        Assert.Contains(poses, x => x.State == SparrowState.Forage && x.Pitch > Angles.DegToRad(20f));
        Assert.All(poses.Where(x => x.State == SparrowState.Idle), x =>
            Assert.True(x.Pitch <= Angles.DegToRad(p.LandPitchDeg) + 1e-4f));
    }

    [Fact]
    public void StaysPutAndKeepsYawWrappedOverLongIdle()
    {
        var brain = NewBrain();

        var poses = Run(brain, Input(Vector3.Zero), 120f);

        Assert.All(poses, x =>
        {
            Assert.InRange(x.Yaw, -MathF.PI, MathF.PI);
            Assert.True(Vector3.Distance(x.Position, Vector3.Zero) < 0.2f);
        });
    }

    [Fact]
    public void HugeFrameDeltaDoesNotTeleport()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(3f, 0f, 0f);

        var before = brain.Tick(Input(target)).Position;
        var after = brain.Tick(Input(target) with { Dt = 5f }).Position;

        Assert.True(Vector3.Distance(before, after) <= p.HopDistanceMax + 1e-3f);
    }

    [Fact]
    public void HopsUpOntoSmallStep()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(0.2f, 0.5f, 0f);

        var poses = Run(brain, Input(target), 10f);

        Assert.Equal(0.5f, poses[^1].Position.Y, 3);
        Assert.DoesNotContain(poses, x => x.Anim == SparrowAnim.Fly);
    }

    [Fact]
    public void FliesToDistantTargetThenLands()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(10f, 0f, 0f);

        var poses = Run(brain, Input(target), 8f);

        Assert.True(poses[0].Triggers.HasFlag(SparrowTrigger.TakeOff));
        Assert.Equal(SparrowAnim.Fly, poses[0].Anim);
        Assert.True(poses.Max(x => x.Position.Y) >= p.FlyAltitudeMin);
        Assert.Contains(poses, x => x.State == SparrowState.Land);

        var flightYs = poses.Where(x => x.State == SparrowState.Fly).Select(x => x.Position.Y).ToList();
        var pairs = flightYs.Zip(flightYs.Skip(1)).ToList();
        Assert.True(pairs.Any(pair => pair.Second > pair.First) && pairs.Any(pair => pair.Second < pair.First),
            "波状飛行で上下すること");

        var final = poses[^1];
        Assert.Equal(SparrowAnim.Idle, final.Anim);
        Assert.Equal(0f, final.Position.Y, 3);
        Assert.True(Vector3.Distance(final.Position, target) <= p.ArriveDistance + p.HopDistanceMax);
    }

    [Fact]
    public void SnapsToTargetAfterTeleport()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var far = new Vector3(100f, 5f, 0f);

        var pose = brain.Tick(Input(far));

        Assert.Equal(far, pose.Position);
        Assert.Equal(SparrowState.Idle, pose.State);
    }

    [Fact]
    public void InvalidateSnapsToTargetOnNextTick()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        brain.Invalidate();
        var target = new Vector3(3f, 0f, 0f);

        Assert.Equal(target, brain.Tick(Input(target)).Position);
    }

    [Fact]
    public void KeepsUpWithFastMountWithoutResetting()
    {
        var p = new SparrowParams();
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var velocity = new Vector3(20f, 0f, 0f);
        var maxGap = 0f;

        for (var i = 1; i <= 600; i++)
        {
            var target = velocity * (i * Dt);
            var pose = brain.Tick(Input(target, target + new Vector3(1.5f, 0f, 0f), velocity, mounted: true));
            maxGap = MathF.Max(maxGap, Angles.HorizontalDistance(pose.Position, target));
        }

        Assert.True(maxGap < p.ResetDistance * 0.5f, $"maxGap={maxGap}");
    }

    [Theory]
    [InlineData(6f, false)]
    [InlineData(9f, true)]
    public void StaysAirborneWhileFollowingFastPlayer(float speed, bool mounted)
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var velocity = new Vector3(speed, 0f, 0f);
        var takeOffs = 0;

        for (var i = 1; i <= 1200; i++)
        {
            var target = velocity * (i * Dt);
            var pose = brain.Tick(Input(target, target + new Vector3(1.5f, 0f, 0f), velocity, mounted));
            if (pose.Triggers.HasFlag(SparrowTrigger.TakeOff))
                takeOffs++;
        }

        Assert.True(takeOffs <= 1, $"takeOffs={takeOffs}");
    }

    [Fact]
    public void LandsOnceFastPlayerStops()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var velocity = new Vector3(9f, 0f, 0f);
        var target = Vector3.Zero;
        for (var i = 1; i <= 300; i++)
        {
            target = velocity * (i * Dt);
            brain.Tick(Input(target, target + new Vector3(1.5f, 0f, 0f), velocity, mounted: true));
        }

        var poses = Run(brain, Input(target, target + new Vector3(1.5f, 0f, 0f), Vector3.Zero, mounted: true), 5f);

        Assert.Equal(SparrowAnim.Idle, poses[^1].Anim);
        Assert.Equal(target.Y, poses[^1].Position.Y, 3);
    }

    [Fact]
    public void FluttersUpOntoHighLedgeWithoutTeleporting()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var target = new Vector3(0.3f, 1.5f, 0f);

        var poses = Run(brain, Input(target), 6f);

        Assert.Contains(poses, x => x.Triggers.HasFlag(SparrowTrigger.TakeOff));
        Assert.All(poses.Zip(poses.Skip(1)), pair =>
            Assert.True(MathF.Abs(pair.Second.Position.Y - pair.First.Position.Y) < 0.15f,
                $"{pair.First.Position.Y} -> {pair.Second.Position.Y}"));
        Assert.Equal(SparrowAnim.Idle, poses[^1].Anim);
        Assert.Equal(1.5f, poses[^1].Position.Y, 3);
    }

    [Fact]
    public void FleesWithAlarmWhenPlayerRushesIn()
    {
        var brain = NewBrain();
        brain.Tick(Input(Vector3.Zero));
        var rush = Input(Vector3.Zero, new Vector3(-1.5f, 0f, 0f), new Vector3(6f, 0f, 0f));

        var first = brain.Tick(rush);
        Assert.Equal(SparrowState.Alarm, first.State);
        Assert.True(first.Triggers.HasFlag(SparrowTrigger.Alarm));

        var poses = Run(brain, rush, 1f);
        Assert.DoesNotContain(poses, x => x.Triggers.HasFlag(SparrowTrigger.Alarm)); // クールダウン中
        Assert.True(poses.Max(x => x.Position.X) > 0.1f, "プレイヤーと反対 (+X) へ逃げること");
    }
}
