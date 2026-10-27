using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 首や尾のボーンを、体の向きの軸で回したときの局所の回転を求める。
/// モデル空間は Y が上、Z が前、X が体の横。ボーンごとの局所の軸の向きに頼らない。
/// </summary>
public static class BoneTwist
{
    /// <param name="parentModel">親ボーンのモデル空間の回転</param>
    /// <param name="boneModel">このボーンの、アニメ計算後のモデル空間の回転</param>
    /// <param name="yaw">上下の軸まわり (正で +X 側へ)</param>
    /// <param name="pitch">横の軸まわり (正で、前を向いた頭は下がり、後ろを向いた尾は上がる)</param>
    /// <param name="roll">前後の軸まわり (首をかしげる)</param>
    public static Quaternion LocalRotation(Quaternion parentModel, Quaternion boneModel, float yaw, float pitch, float roll)
    {
        var twist = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll);
        return Quaternion.Normalize(Quaternion.Inverse(parentModel) * twist * boneModel);
    }
}
