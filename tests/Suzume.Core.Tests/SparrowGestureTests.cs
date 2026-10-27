using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class SparrowGestureTests
{
    private const float Dt = 1f / 60f;
    private static readonly Vector3 Player = Vector3.Zero;
    private static readonly Vector3 Target = new(0.9f, 0f, -1.1f);

    private static SparrowParams Calm() => new()
    {
        WanderDelay = float.PositiveInfinity,
        DustBathIntervalMin = float.PositiveInfinity,
        DustBathIntervalMax = float.PositiveInfinity,
        ForageChance = 0f,
        PreenChance = 0f,
    };

    private static SparrowInput Input(Vector3? target = null) =>
        new(Dt, target ?? Target, 0f, Player, Vector3.Zero, false, Player + new Vector3(0f, 1.7f, 0f));

    private static List<SparrowPose> Run(SparrowBrain brain, float seconds, Vector3? target = null)
    {
        var poses = new List<SparrowPose>();
        for (var i = 0; i < (int)MathF.Round(seconds / Dt); i++)
            poses.Add(brain.Tick(Input(target)));
        return poses;
    }

    private static float Deg(float degrees) => Angles.DegToRad(degrees);

    [Fact]
    public void TiltsHeadWhileLookingAround()
    {
        var p = Calm();
        p.HeadTiltChance = 1f;
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, 10f);

        Assert.Contains(poses, x => MathF.Abs(x.NeckRoll) > Deg(15f));
        Assert.Contains(poses, x => x.NeckRoll > Deg(10f));
        Assert.Contains(poses, x => x.NeckRoll < -Deg(10f));
    }

    // 首をかしげるときは「クイッ」と素早く: 動き始めて 0.08 秒 (5 フレーム) で、かしげ切る角度の 8 割に届く
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TiltsHeadWithAQuickSnap(int seed)
    {
        var p = Calm();
        p.HeadTiltChance = 1f;
        var brain = new SparrowBrain(p, new SeededRandom(seed));

        var poses = Run(brain, 10f);

        var checkedTilts = 0;
        for (var i = 1; i + 30 < poses.Count; i++)
        {
            // 正面からかしげ始めたフレーム
            if (MathF.Abs(poses[i - 1].NeckRoll) > 0.001f || MathF.Abs(poses[i].NeckRoll) < Deg(1f))
                continue;

            var peak = poses.Skip(i).Take(30).Max(x => MathF.Abs(x.NeckRoll));
            Assert.True(MathF.Abs(poses[i + 4].NeckRoll) >= peak * 0.8f,
                $"{i} フレーム目: 5 フレーム後 {poses[i + 4].NeckRoll * 180f / MathF.PI:F1}° / 最大 {peak * 180f / MathF.PI:F1}°");
            checkedTilts++;
        }

        Assert.True(checkedTilts > 0, "首をかしげなかった");
    }

    [Fact]
    public void FlicksTailUpOnLanding()
    {
        var brain = new SparrowBrain(Calm(), new SeededRandom(1));
        brain.Tick(Input(Vector3.Zero));

        var poses = Run(brain, 8f, new Vector3(10f, 0f, 0f));

        var landed = poses.FindIndex(poses.FindIndex(x => x.State == SparrowState.Land), x => x.State != SparrowState.Land);
        Assert.True(landed > 0);
        var afterLanding = poses.Skip(landed).Take(30).ToList();
        Assert.Contains(afterLanding, x => x.TailLift > Deg(25f));
        // 跳ね上げは一瞬で、0.25 秒で元に戻る (そのあとのホップでまた小さく跳ねることはある)
        Assert.Equal(0f, poses[landed + 16].TailLift, 3);
    }

    [Fact]
    public void FlicksTailALittleAfterEachHop()
    {
        var brain = new SparrowBrain(Calm(), new SeededRandom(1));
        brain.Tick(Input(Vector3.Zero));

        var poses = Run(brain, 6f, new Vector3(2f, 0f, 0f));

        Assert.Contains(poses, x => x.TailLift is > 0.05f and < 0.4f);
    }

    [Fact]
    public void PreensItsBackAndItsWing()
    {
        var p = Calm();
        p.PreenChance = 1f;
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, 40f);

        var preen = poses.Where(x => x.State == SparrowState.Preen).ToList();
        Assert.NotEmpty(preen);
        // 首を後ろへひねる
        Assert.Contains(preen, x => MathF.Abs(x.NeckYaw) > Deg(60f));
        // くちばしでつつく (首を細かく上下させる)
        Assert.True(preen.Max(x => x.NeckPitch) - preen.Min(x => x.NeckPitch) > Deg(8f));
        // 翼の手入れでは、片側の翼だけを持ち上げる
        Assert.Contains(preen, x => x.WingSide != 0f && x.WingSpread > Deg(20f));
        // 背中の手入れでは翼は開かない
        Assert.Contains(preen, x => x.WingSpread < Deg(1f) && MathF.Abs(x.NeckYaw) > Deg(60f));
    }

    [Fact]
    public void RequestedPreenStartsAtNextIdle()
    {
        var p = Calm();
        var brain = new SparrowBrain(p, new SeededRandom(1));
        brain.Tick(Input());

        brain.RequestPreen();
        var poses = Run(brain, p.IdleMax + 0.5f);

        Assert.Contains(poses, x => x.State == SparrowState.Preen);
    }

    [Fact]
    public void NeckReturnsAfterPreening()
    {
        var p = Calm();
        p.PreenChance = 1f;
        var brain = new SparrowBrain(p, new SeededRandom(1));

        var poses = Run(brain, 15f);

        var end = poses.FindIndex(poses.FindIndex(x => x.State == SparrowState.Preen), x => x.State != SparrowState.Preen);
        Assert.True(end > 0);
        var settled = poses[Math.Min(poses.Count - 1, end + 30)];
        Assert.InRange(settled.NeckYaw, -Deg(2f), Deg(2f));
        Assert.InRange(settled.WingSpread, 0f, Deg(2f));
    }
}
