using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowGroundTests
{
    private const float Dt = 1f / 60f;

    // +X へ登る斜面 (高さ = 0.5 × X)
    private static float? Slope(Vector3 at) => 0.5f * at.X;

    private static readonly Vector3 Player = Vector3.Zero;
    private static readonly Vector3 Target = new(0.9f, 0.45f, -1.1f);

    private static SparrowInput Input(bool threatened = false) =>
        new(Dt, Target, MathF.PI / 2f, Player, Vector3.Zero, false, Player + new Vector3(0f, 1.7f, 0f), false, false, threatened);

    private static List<SparrowPose> Run(SparrowBrain brain, float seconds)
    {
        var poses = new List<SparrowPose>();
        for (var i = 0; i < (int)MathF.Round(seconds / Dt); i++)
            poses.Add(brain.Tick(Input()));
        return poses;
    }

    [Fact]
    public void WandersAlongSlopeWithoutFloating()
    {
        var p = new SparrowParams
        {
            DustBathIntervalMin = float.PositiveInfinity,
            DustBathIntervalMax = float.PositiveInfinity,
            ForageChance = 0f,
            PreenChance = 0f,
        };
        var brain = new SparrowBrain(p, new SeededRandom(1), Slope);

        var poses = Run(brain, 40f);

        Assert.True(poses.Max(x => Angles.HorizontalDistance(x.Position, Target)) > 0.8f, "動き回っていること");
        Assert.All(poses.Where(x => x.State == SparrowState.Idle), x =>
            Assert.True(x.Position.Y - Slope(x.Position)!.Value is > -0.05f and < 0.05f + 0.04f,
                $"({x.Position.X:F2}, {x.Position.Z:F2}) の高さ {x.Position.Y:F2}、地面 {Slope(x.Position):F2}"));
    }

    [Fact]
    public void LandsOnSlopeAfterBeingScared()
    {
        var p = new SparrowParams
        {
            WanderDelay = float.PositiveInfinity,
            DustBathIntervalMin = float.PositiveInfinity,
            DustBathIntervalMax = float.PositiveInfinity,
        };
        var brain = new SparrowBrain(p, new SeededRandom(1), Slope);
        brain.Tick(Input());
        brain.Tick(Input(threatened: true));

        var poses = Run(brain, 4f);

        var landed = poses[^1];
        Assert.DoesNotContain(landed.State, new[] { SparrowState.TakeOff, SparrowState.Fly, SparrowState.Land });
        Assert.True(MathF.Abs(landed.Position.Y - Slope(landed.Position)!.Value) < 0.05f,
            $"高さ {landed.Position.Y:F2}、地面 {Slope(landed.Position):F2}");
    }

    // 3 < X < 6 に高さ 1.5 の台がある平地
    private static float? Mound(Vector3 at) => at.X is > 3f and < 6f ? 1.5f : 0f;

    // X > 1 から高さ 0.3 の段差
    private static float? Step(Vector3 at) => at.X > 1f ? 0.3f : 0f;

    private static List<SparrowPose> RunTo(SparrowBrain brain, Vector3 target, Vector3 player, float seconds)
    {
        var poses = new List<SparrowPose>();
        for (var i = 0; i < (int)MathF.Round(seconds / Dt); i++)
            poses.Add(brain.Tick(new SparrowInput(Dt, target, 0f, player, Vector3.Zero, false)));
        return poses;
    }

    private static SparrowParams Calm() => new()
    {
        WanderDelay = float.PositiveInfinity,
        DustBathIntervalMin = float.PositiveInfinity,
        DustBathIntervalMax = float.PositiveInfinity,
    };

    [Fact]
    public void FliesOverMoundWithoutSinkingIntoIt()
    {
        var brain = new SparrowBrain(Calm(), new SeededRandom(1), Mound);
        brain.Tick(new SparrowInput(Dt, Vector3.Zero, 0f, new Vector3(0f, 0f, -3f), Vector3.Zero, false));
        var target = new Vector3(10f, 0f, 0f);

        var poses = RunTo(brain, target, target + new Vector3(0f, 0f, -3f), 8f);

        Assert.Contains(poses, x => x.State == SparrowState.Fly);
        Assert.All(poses, x => Assert.True(x.Position.Y >= Mound(x.Position)!.Value - 0.01f,
            $"x={x.Position.X:F2} の高さ {x.Position.Y:F2} が地面 {Mound(x.Position)} の下 ({x.State})"));
        GroundAssert.OnGround(0f, poses[^1]);
    }

    [Fact]
    public void LandsOnTopOfRaisedGround()
    {
        var brain = new SparrowBrain(Calm(), new SeededRandom(1), Mound);
        brain.Tick(new SparrowInput(Dt, Vector3.Zero, 0f, new Vector3(0f, 0f, -3f), Vector3.Zero, false));
        var target = new Vector3(4.5f, 1.5f, 0f);

        var poses = RunTo(brain, target, target + new Vector3(0f, 0f, -0.5f), 8f);

        Assert.All(poses, x => Assert.True(x.Position.Y >= Mound(x.Position)!.Value - 0.01f,
            $"x={x.Position.X:F2} の高さ {x.Position.Y:F2} が地面 {Mound(x.Position)} の下 ({x.State})"));
        GroundAssert.OnGround(1.5f, poses[^1]);
    }

    [Fact]
    public void StaysOnStepAfterWandering()
    {
        var p = new SparrowParams { DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
        var brain = new SparrowBrain(p, new SeededRandom(1), Step);

        // プレイヤーは段差の境目 (X = 1) に立っているので、周りを渡り歩くと段差の上下を行き来する
        var poses = RunTo(brain, new Vector3(0.9f, 0f, -1.1f), new Vector3(1f, 0f, 0f), 40f);

        var grounded = poses.Where(x => x.State is SparrowState.Idle or SparrowState.Forage or SparrowState.Preen).ToList();
        Assert.Contains(grounded, x => x.Position.X > 1.2f); // 段差の上にも行く
        // 地面に接している (向きを変えるときに小さく跳ねる分だけは上がってよい)
        Assert.All(grounded, x => Assert.InRange(x.Position.Y - Step(x.Position)!.Value, -0.02f, p.TurnHopHeight + 0.01f));
    }

    [Fact]
    public void FallsBackToTargetHeightWhenGroundUnknown()
    {
        var p = new SparrowParams { DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
        var brain = new SparrowBrain(p, new SeededRandom(1), _ => null);

        var poses = Run(brain, 20f);

        Assert.All(poses.Where(x => x.State == SparrowState.Idle), x => Assert.InRange(x.Position.Y - Target.Y, -0.001f, p.TurnHopHeight + 0.001f));
    }
}
