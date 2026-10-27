using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 翼を広げてばたつかせるためのボーンの回転を、モデル空間の形から計算する。
/// ボーンごとの局所の軸の向きに頼らず、「体の外側へ開く」「上へ振る」を実際の向きで決める。
/// モデル空間は Y が上、Z が前、X が体の横。
/// </summary>
public static class WingPose
{
    /// <param name="parentModel">親ボーン (背中など) のモデル空間の回転</param>
    /// <param name="boneModel">翼の付け根のボーンの、アニメ計算後のモデル空間の回転</param>
    /// <param name="shoulder">翼の付け根のモデル空間の位置 (X の符号で左右を決める)</param>
    /// <param name="boneToChild">付け根から翼の先のボーンへのモデル空間の向き</param>
    /// <param name="spread">外側へ開く角度 (ラジアン)</param>
    /// <param name="flap">上へ振る角度 (ラジアン、負なら下へ)</param>
    /// <returns>翼の付け根のボーンに入れる局所の回転</returns>
    public static Quaternion LocalRotation(
        Quaternion parentModel, Quaternion boneModel, Vector3 shoulder, Vector3 boneToChild, float spread, float flap)
    {
        var side = shoulder.X < 0f ? -1f : 1f;

        // 垂直の軸まわりに、翼の先が体の外側へ出る向きに回す(左右の符号は実際に外へ出る方を選ぶ)
        var spreadPlus = Quaternion.CreateFromAxisAngle(Vector3.UnitY, spread);
        var spreadMinus = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -spread);
        var spreadRotation = side * Vector3.Transform(boneToChild, spreadPlus).X >= side * Vector3.Transform(boneToChild, spreadMinus).X
            ? spreadPlus
            : spreadMinus;

        // 前後の軸まわりに回して上下に振る。外側へ出た翼の先は side * flap の回転で上がる
        var flapRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, side * flap);

        var newModel = flapRotation * spreadRotation * boneModel;
        return Quaternion.Normalize(Quaternion.Inverse(parentModel) * newModel);
    }
}
