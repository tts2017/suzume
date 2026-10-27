using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class ProbeOriginTests
{
    [Fact]
    public void StartsAbovePlayerFeetForLowPoints() =>
        Assert.Equal(new Vector3(1f, 12f, 3f), ProbeOrigin.For(new Vector3(1f, 4f, 3f), referenceY: 10f, startAbove: 2f));

    [Fact]
    public void StartsAboveThePointWhenItIsHigherThanThePlayer()
    {
        // プレイヤーは下の地面 (0)、スズメは高さ 3 の台の上: 台の表面より上から探さないと台をすり抜ける
        Assert.Equal(new Vector3(1f, 5f, 3f), ProbeOrigin.For(new Vector3(1f, 3f, 3f), referenceY: 0f, startAbove: 2f));
    }
}
