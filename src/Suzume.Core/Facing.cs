using System.Numerics;

namespace Suzume.Core;

public static class Facing
{
    // これより近ければ向きに関係なく「目の前」とみなす(頭の上・足元)
    private const float Touching = 0.5f;

    /// <summary>point が、yaw を向いた from の前方 (距離 maxDistance 以内、左右 halfAngleDeg 以内) にあるか。高さは見ない。</summary>
    public static bool IsInFront(Vector3 from, float yaw, Vector3 point, float maxDistance, float halfAngleDeg)
    {
        var toPoint = Angles.Horizontal(point - from);
        var distance = toPoint.Length();
        if (distance < Touching)
            return true;
        if (distance > maxDistance)
            return false;

        var forward = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        return Vector3.Dot(forward, toPoint / distance) >= MathF.Cos(Angles.DegToRad(halfAngleDeg));
    }
}
