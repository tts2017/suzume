using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class FacingTests
{
    // yaw = π/2 のプレイヤーは +X を向いている
    private const float FacingPlusX = MathF.PI / 2f;

    [Theory]
    [InlineData(3f, 0f, true)]    // 正面 3y
    [InlineData(3f, 2f, true)]    // 斜め前 (約 34°)
    [InlineData(3f, 3f, false)]   // 45° は範囲外
    [InlineData(-3f, 0f, false)]  // 真後ろ
    [InlineData(7f, 0f, false)]   // 遠すぎる
    public void DetectsPointInFrontCone(float x, float z, bool expected) =>
        Assert.Equal(expected, Facing.IsInFront(Vector3.Zero, FacingPlusX, new Vector3(x, 0f, z), 6f, 40f));

    [Fact]
    public void IgnoresHeightDifference() =>
        Assert.True(Facing.IsInFront(Vector3.Zero, FacingPlusX, new Vector3(3f, 1.7f, 0f), 6f, 40f));

    [Fact]
    public void PointOnTopOfPlayerCounts() =>
        Assert.True(Facing.IsInFront(Vector3.Zero, FacingPlusX, new Vector3(0f, 1.7f, 0f), 6f, 40f));
}
