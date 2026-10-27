using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowSmoothnessTests
{
    private const float Dt = 1f / 60f;
    // 1 フレーム (60fps) で許す変化。これを超えるとワープして見える
    private static readonly float MaxYawStep = Angles.DegToRad(20f);
    private static readonly float MaxTiltStep = Angles.DegToRad(15f);

    private static readonly Vector3 Player = Vector3.Zero;
    private static readonly Vector3 Target = new(0.9f, 0f, -1.1f);

    private static SparrowInput Input(Vector3? target = null, Vector3? player = null, Vector3? velocity = null) =>
        new(Dt, target ?? Target, 0f, player ?? Player, velocity ?? Vector3.Zero, false, (player ?? Player) + new Vector3(0f, 1.7f, 0f));

    private static void AssertNoJumps(IReadOnlyList<SparrowPose> poses)
    {
        foreach (var (prev, next) in poses.Zip(poses.Skip(1)))
        {
            Assert.True(MathF.Abs(Angles.Wrap(next.Yaw - prev.Yaw)) <= MaxYawStep,
                $"向きが 1 フレームで {Angles.Wrap(next.Yaw - prev.Yaw) * 180f / MathF.PI:F1}° 変わった ({prev.State} -> {next.State})");
            Assert.True(MathF.Abs(next.Pitch - prev.Pitch) <= MaxTiltStep,
                $"前傾が 1 フレームで {(next.Pitch - prev.Pitch) * 180f / MathF.PI:F1}° 変わった ({prev.State} -> {next.State})");
            Assert.True(MathF.Abs(next.WingSpread - prev.WingSpread) <= MaxTiltStep,
                $"翼の開きが 1 フレームで {(next.WingSpread - prev.WingSpread) * 180f / MathF.PI:F1}° 変わった ({prev.State} -> {next.State})");
        }
    }

    [Fact]
    public void NoWarpingTurnsWhileLivingAroundStillPlayer()
    {
        var brain = new SparrowBrain(new SparrowParams { DustBathIntervalMin = 5f, DustBathIntervalMax = 10f }, new SeededRandom(1));
        brain.Tick(Input());

        var poses = new List<SparrowPose>();
        for (var i = 0; i < 60 * 60; i++)
            poses.Add(brain.Tick(Input()));

        Assert.Contains(poses, x => x.State == SparrowState.DustBath);
        AssertNoJumps(poses);
    }

    [Fact]
    public void NoWarpingTurnsWhileFollowingAndFlying()
    {
        var brain = new SparrowBrain(new SparrowParams(), new SeededRandom(1));
        brain.Tick(Input());
        var player = Player;

        var poses = new List<SparrowPose>();
        for (var i = 1; i <= 60 * 15; i++)
        {
            // 走って、途中で向きを変えて走り、最後は止まる
            var velocity = i < 60 * 5 ? new Vector3(6f, 0f, 2f) : i < 60 * 10 ? new Vector3(-2f, 0f, 6f) : Vector3.Zero;
            player += velocity * Dt;
            poses.Add(brain.Tick(Input(target: player + Target, player: player, velocity: velocity)));
        }

        Assert.Contains(poses, x => x.State == SparrowState.Fly);
        AssertNoJumps(poses);
    }

    [Fact]
    public void TurnsAroundWithALittleHopInPlace()
    {
        var p = new SparrowParams
        {
            WanderDelay = float.PositiveInfinity,
            DustBathIntervalMin = float.PositiveInfinity,
            DustBathIntervalMax = float.PositiveInfinity,
            ForageChance = 0f,
            PreenChance = 0f,
            ScanAngleMinDeg = 50f,
            ScanAngleMaxDeg = 70f,
        };
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());

        var poses = new List<SparrowPose>();
        for (var i = 0; i < 60 * 5; i++)
            poses.Add(brain.Tick(Input()));

        // その場 (水平位置は変えず) で、少し跳ねながら向きを変える
        Assert.All(poses, x => Assert.Equal(0f, Angles.HorizontalDistance(x.Position, Target), 4));
        Assert.Contains(poses, x => x.Position.Y > 0.02f);
        AssertNoJumps(poses);
    }
}
