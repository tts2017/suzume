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
            Assert.True(MathF.Abs(x.Position.Y - Slope(x.Position)!.Value) < 0.05f,
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

    [Fact]
    public void FallsBackToTargetHeightWhenGroundUnknown()
    {
        var p = new SparrowParams { DustBathIntervalMin = float.PositiveInfinity, DustBathIntervalMax = float.PositiveInfinity };
        var brain = new SparrowBrain(p, new SeededRandom(1), _ => null);

        var poses = Run(brain, 20f);

        Assert.All(poses.Where(x => x.State == SparrowState.Idle), x => Assert.Equal(Target.Y, x.Position.Y, 3));
    }
}
