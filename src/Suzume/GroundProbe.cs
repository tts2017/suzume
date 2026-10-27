using System.Numerics;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using Suzume.Core;

namespace Suzume;

/// <summary>地形への真下向きのレイキャストで、その場所の地面の高さを取る。</summary>
public sealed class GroundProbe
{
    // 基準の高さ(プレイヤーの足元)か調べる地点の高い方より、これだけ上から下へ探す
    private const float StartAbove = 2f;
    private const float MaxDistance = 8f;

    /// <summary>探し始める基準の高さ。毎フレーム、プレイヤーの足元の高さを入れる。</summary>
    public float ReferenceY { get; set; }

    private readonly GroundCalibration calibration = new();

    /// <summary>見た目の地面の高さ(当たり判定にキャラクターの浮きを足したもの)。見つからなければ null。</summary>
    public float? GroundAt(Vector3 at) =>
        CollisionAt(at) is { } collision ? calibration.Apply(collision) : null;

    /// <summary>プレイヤーが当たり判定からどれだけ浮いて立っているかを測る。地面に立っているときだけ呼ぶ。</summary>
    public void Calibrate(Vector3 playerFeet, bool grounded) =>
        calibration.Update(playerFeet.Y, grounded ? CollisionAt(playerFeet) : null, grounded);

    private float? CollisionAt(Vector3 at)
    {
        var origin = ProbeOrigin.For(at, ReferenceY, StartAbove);
        return BGCollisionModule.RaycastMaterialFilter(origin, -Vector3.UnitY, out var hit, MaxDistance)
            ? hit.Point.Y
            : null;
    }
}
