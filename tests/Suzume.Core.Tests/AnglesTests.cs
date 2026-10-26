using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class AnglesTests
{
    [Fact]
    public void YawTowardsPlusZIsZero() =>
        Assert.Equal(0f, Angles.YawTowards(Vector3.Zero, Vector3.UnitZ), 5);

    [Fact]
    public void YawTowardsPlusXIsHalfPi() =>
        Assert.Equal(MathF.PI / 2f, Angles.YawTowards(Vector3.Zero, Vector3.UnitX), 5);

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(4f, 4f - MathF.Tau)]
    [InlineData(-4f, -4f + MathF.Tau)]
    [InlineData(20f, 20f - 3f * MathF.Tau)]
    public void WrapKeepsAngleWithinPi(float angle, float expected) =>
        Assert.Equal(expected, Angles.Wrap(angle), 4);

    [Fact]
    public void HorizontalDistanceIgnoresHeight() =>
        Assert.Equal(5f, Angles.HorizontalDistance(new Vector3(0f, 5f, 0f), new Vector3(3f, 0f, 4f)), 5);

    [Fact]
    public void DegToRadConverts() =>
        Assert.Equal(MathF.PI, Angles.DegToRad(180f), 5);
}
