using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 角度と水平距離の小さな関数。yaw は FFXIV と同じく 0 で +Z を向き、前方は (sin, 0, cos)。
/// </summary>
public static class Angles
{
    public static float YawTowards(Vector3 from, Vector3 to) =>
        MathF.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>角度を -π〜π に収める。</summary>
    public static float Wrap(float angle) =>
        MathF.IEEERemainder(angle, MathF.Tau);

    public static float DegToRad(float degrees) => degrees * MathF.PI / 180f;

    public static Vector3 Horizontal(Vector3 v) => new(v.X, 0f, v.Z);

    public static float HorizontalDistance(Vector3 a, Vector3 b) => Horizontal(a - b).Length();
}
