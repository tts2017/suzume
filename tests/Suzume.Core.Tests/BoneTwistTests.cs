using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class BoneTwistTests
{
    // モデル空間は Y が上、Z が前、X が体の横
    private static Vector3 Rotate(Vector3 direction, Quaternion parent, float yaw, float pitch, float roll)
    {
        var boneModel = parent; // 局所の回転は単位
        var local = BoneTwist.LocalRotation(parent, boneModel, yaw, pitch, roll);
        var localDirection = Vector3.Transform(direction, Quaternion.Inverse(boneModel));
        return Vector3.Transform(localDirection, parent * local);
    }

    [Fact]
    public void PositivePitchLiftsATailThatPointsBackward()
    {
        var tail = Rotate(-Vector3.UnitZ, Quaternion.Identity, 0f, Angles.DegToRad(35f), 0f);
        Assert.True(tail.Y > 0.5f, $"尾の先 y={tail.Y}");
    }

    [Fact]
    public void PositivePitchLowersAHeadThatPointsForward()
    {
        var head = Rotate(Vector3.UnitZ, Quaternion.Identity, 0f, Angles.DegToRad(30f), 0f);
        Assert.True(head.Y < -0.4f, $"頭 y={head.Y}");
    }

    [Fact]
    public void YawTurnsTheHeadTowardThatSide()
    {
        // 正の yaw は +X 側へ回す (FFXIV の yaw と同じ向き)
        var head = Rotate(Vector3.UnitZ, Quaternion.Identity, Angles.DegToRad(90f), 0f, 0f);
        Assert.Equal(1f, head.X, 3);
    }

    [Fact]
    public void RollTiltsAnUprightHeadSideways()
    {
        var up = Rotate(Vector3.UnitY, Quaternion.Identity, 0f, 0f, Angles.DegToRad(30f));
        Assert.True(MathF.Abs(up.X) > 0.4f, $"x={up.X}");
    }

    [Fact]
    public void WorksWhenParentIsRotated()
    {
        var parent = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.3f);
        var tail = Rotate(-Vector3.UnitZ, parent, 0f, Angles.DegToRad(35f), 0f);
        Assert.True(tail.Y > 0.5f, $"y={tail.Y}");
    }

    [Fact]
    public void ZeroKeepsTheAnimationPose()
    {
        var parent = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f);
        var boneModel = parent * Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f);
        var local = BoneTwist.LocalRotation(parent, boneModel, 0f, 0f, 0f);
        Assert.True(MathF.Abs(Quaternion.Dot(local, Quaternion.Inverse(parent) * boneModel)) > 0.9999f);
    }
}
