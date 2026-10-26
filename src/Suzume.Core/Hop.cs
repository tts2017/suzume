using System.Numerics;

namespace Suzume.Core;

/// <summary>両足ホップ 1 回分の放物線。time は開始からの秒数。</summary>
public readonly record struct Hop(Vector3 From, Vector3 To, float Height, float Duration)
{
    public Vector3 Evaluate(float time)
    {
        var u = Math.Clamp(time / Duration, 0f, 1f);
        var point = Vector3.Lerp(From, To, u);
        point.Y += 4f * Height * u * (1f - u);
        return point;
    }
}
