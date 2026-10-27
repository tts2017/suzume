using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class WingPoseTests
{
    // 畳んだ翼: 肩は体の横 (左は +X、右は -X)、翼の先は後ろ (-Z) を向いている
    private static readonly Vector3 LeftShoulder = new(0.02f, 0.05f, 0f);
    private static readonly Vector3 RightShoulder = new(-0.02f, 0.05f, 0f);
    private static readonly Vector3 Folded = new(0f, 0f, -0.05f);

    private static Vector3 TipAfter(Vector3 shoulder, Quaternion parentModel, float spread, float flap)
    {
        var boneModel = parentModel; // 局所の回転は単位
        var local = WingPose.LocalRotation(parentModel, boneModel, shoulder, Folded, spread, flap);
        // 新しいモデル空間の回転で、元の翼の向き (局所) を回す
        var newModel = parentModel * local;
        var localDirection = Vector3.Transform(Folded, Quaternion.Inverse(boneModel));
        return Vector3.Transform(localDirection, newModel);
    }

    [Fact]
    public void SpreadSwingsLeftWingOutward()
    {
        var tip = TipAfter(LeftShoulder, Quaternion.Identity, Angles.DegToRad(60f), 0f);
        Assert.True(tip.X > 0.04f, $"左の翼の先 x={tip.X}");
        Assert.Equal(0.05f, tip.Length(), 4);
    }

    [Fact]
    public void SpreadSwingsRightWingOutward()
    {
        var tip = TipAfter(RightShoulder, Quaternion.Identity, Angles.DegToRad(60f), 0f);
        Assert.True(tip.X < -0.04f, $"右の翼の先 x={tip.X}");
    }

    [Fact]
    public void PositiveFlapRaisesSpreadWings()
    {
        var left = TipAfter(LeftShoulder, Quaternion.Identity, Angles.DegToRad(60f), Angles.DegToRad(30f));
        var right = TipAfter(RightShoulder, Quaternion.Identity, Angles.DegToRad(60f), Angles.DegToRad(30f));
        Assert.True(left.Y > 0.01f, $"左 y={left.Y}");
        Assert.True(right.Y > 0.01f, $"右 y={right.Y}");
    }

    [Fact]
    public void WorksWhenParentIsRotated()
    {
        // 親 (背中) が向きを変えていても、モデル空間で外側へ開く
        var parent = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f);
        var tip = TipAfter(LeftShoulder, parent, Angles.DegToRad(60f), 0f);
        Assert.True(tip.X > 0.04f, $"x={tip.X}");
    }

    [Fact]
    public void ZeroAnglesKeepTheAnimationPose()
    {
        var parent = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f);
        var boneModel = parent * Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f);
        var local = WingPose.LocalRotation(parent, boneModel, LeftShoulder, Folded, 0f, 0f);
        var expected = Quaternion.Inverse(parent) * boneModel;
        Assert.True(MathF.Abs(Quaternion.Dot(local, expected)) > 0.9999f);
    }
}
