using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowBrainTests
{
    private const float Dt = 1f / 60f;

    private static SparrowBrain NewBrain(SparrowParams? parameters = null, int seed = 1) =>
        new(parameters ?? new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity }, new SeededRandom(seed));

    // プレイヤーは既定で論理位置の 3y 後ろに静止している(警戒の範囲外)
    // 頭の位置は既定でプレイヤーの 1.7y 上
    private static SparrowInput Input(
        Vector3 target,
        Vector3? playerPosition = null,
        Vector3? playerVelocity = null,
        bool mounted = false,
        bool perch = false,
        bool jumping = false)
    {
        var player = playerPosition ?? target + new Vector3(0f, 0f, -3f);
        return new(Dt, target, 0f, player, playerVelocity ?? Vector3.Zero, mounted, player + new Vector3(0f, 1.7f, 0f), perch, jumping);
    }

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
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
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
    public void LooksAroundWithQuickTurnsWhileIdle()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity, ForageChance = 0f, PreenChance = 0f };
        var brain = NewBrain(p);

        var poses = Run(brain, Input(Vector3.Zero), 5f);

        // 0.25 秒ごとに見て、大きく向きが変わっている回数を数える
        var samples = poses.Where((_, i) => i % 15 == 0).ToList();
        var turns = samples.Zip(samples.Skip(1)).Count(pair =>
            MathF.Abs(Angles.Wrap(pair.Second.Yaw - pair.First.Yaw)) >= Angles.DegToRad(p.ScanAngleMinDeg) * 0.8f);
        Assert.True(turns >= 3, $"turns={turns}");
        // その場で向きを変える(水平には動かない)
        Assert.All(poses, x => Assert.Equal(0f, Angles.HorizontalDistance(x.Position, Vector3.Zero), 4));
    }

    [Fact]
    public void ForagingPecksDownAndRecovers()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity, ForageChance = 1f };
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
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
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

        GroundAssert.OnGround(0.5f, poses[^1]);
        Assert.DoesNotContain(poses, x => x.Anim == SparrowAnim.Fly);
    }

    [Fact]
    public void FliesToDistantTargetThenLands()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
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
        GroundAssert.OnGround(0f, final);
        Assert.True(Vector3.Distance(final.Position, target) <= p.ArriveDistance + p.HopDistanceMax);
    }

    private static List<SparrowPose> FlyTenYalms(SparrowParams p)
    {
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        return Run(brain, Input(new Vector3(10f, 0f, 0f)), 8f);
    }

    [Fact]
    public void GlidesDownBeforeLanding()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
        var poses = FlyTenYalms(p);

        var landStart = poses.FindIndex(x => x.State == SparrowState.Land);
        Assert.True(landStart > 0);
        // 着地に入る時点で、すでに低く滑空している(波状飛行の高さから一気に落ちない)
        Assert.True(poses[landStart].Position.Y <= p.GlideSlope * p.LandDistance + 0.05f,
            $"着地開始の高さ {poses[landStart].Position.Y}");
    }

    [Fact]
    public void TouchesDownSoftly()
    {
        var poses = FlyTenYalms(new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity });

        var landing = poses.Where(x => x.State == SparrowState.Land).ToList();
        var drops = landing.Zip(landing.Skip(1)).Select(pair => pair.First.Position.Y - pair.Second.Position.Y).ToList();
        Assert.All(drops, drop => Assert.True(drop < 0.03f, $"1 フレームの降下 {drop}"));
    }

    [Fact]
    public void FlaresNoseUpWhileLanding()
    {
        var poses = FlyTenYalms(new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity });

        Assert.Contains(poses, x => x.State == SparrowState.Land && x.Pitch < -Angles.DegToRad(5f));
    }

    [Fact]
    public void LandsShortThenHopsToTarget()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
        var target = new Vector3(10f, 0f, 0f);
        var poses = FlyTenYalms(p);

        var landEnd = poses.FindLastIndex(x => x.State == SparrowState.Land);
        Assert.True(Vector3.Distance(poses[landEnd + 1].Position, target) > 0.1f, "目標の手前に降りる");
        Assert.True(Vector3.Distance(poses[^1].Position, target) <= p.ArriveDistance + p.HopDistanceMax);
    }

    [Fact]
    public void SettlesSoftlyWhenRunningPlayerStops()
    {
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
        var brain = NewBrain(p);
        brain.Tick(Input(Vector3.Zero));
        var velocity = new Vector3(6f, 0f, 0f);
        var target = Vector3.Zero;
        for (var i = 1; i <= 180; i++)
        {
            target = velocity * (i * Dt);
            brain.Tick(Input(target, target + new Vector3(1.5f, 0f, 0f), velocity));
        }

        // 真横を飛んでいる状態でプレイヤーが止まる
        var poses = Run(brain, Input(target, target + new Vector3(1.5f, 0f, 0f), Vector3.Zero), 5f);

        var landing = poses.Where(x => x.State == SparrowState.Land).ToList();
        Assert.NotEmpty(landing);
        Assert.True(landing[0].Position.Y <= p.GlideSlope * p.LandDistance + 0.05f, $"着地開始の高さ {landing[0].Position.Y}");
        // 飛行と着地の間の降下だけを見る(着地後のホップは放物線なので終わりが速い)
        var airborne = poses.Where(x => x.State is SparrowState.Fly or SparrowState.Land).ToList();
        Assert.All(airborne.Zip(airborne.Skip(1)), pair =>
            Assert.True(pair.First.Position.Y - pair.Second.Position.Y < 0.03f, $"1 フレームの降下 {pair.First.Position.Y - pair.Second.Position.Y}"));
        Assert.Equal(SparrowAnim.Idle, poses[^1].Anim);
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
        var p = new SparrowParams { WanderDelay = float.PositiveInfinity, DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
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
        GroundAssert.OnGround(target.Y, poses[^1]);
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
        GroundAssert.OnGround(1.5f, poses[^1]);
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
