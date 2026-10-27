using System.Numerics;

namespace Suzume.Core;

/// <summary>地面を探す真下向きレイキャストの開始位置。</summary>
public static class ProbeOrigin
{
    /// <summary>
    /// プレイヤーの足元と調べる地点の高い方から startAbove 上。
    /// プレイヤーより高い台の上を調べるとき、台の表面より下から始めてすり抜けないようにする。
    /// </summary>
    public static Vector3 For(Vector3 at, float referenceY, float startAbove) =>
        at with { Y = MathF.Max(referenceY, at.Y) + startAbove };
}
