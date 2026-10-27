using System.Numerics;

namespace Suzume.Core;

/// <summary>左右の耳それぞれの音量と遅れ (サンプル数)、後ろからの度合い (0 で前か横、1 で真後ろ)。</summary>
public readonly record struct StereoPlacement(float LeftGain, float RightGain, int LeftDelaySamples, int RightDelaySamples, float Behind)
{
    public static readonly StereoPlacement Centre = new(1f, 1f, 0, 0, 0f);
}

/// <summary>聞き手から見た鳴き声の向きで、左右の音量差と時間差 (位相差)、後ろからのこもり具合を決める。</summary>
public static class ChirpStereo
{
    // 真横でも反対の耳から少し聞こえるよう、遠い側の耳を最大でこれだけ下げる
    public const float MaxPan = 0.7f;
    // 頭の幅ぶんの到達時間差 (約 0.66ms、44.1kHz)
    public const int MaxDelaySamples = 29;
    // これより近ければ向きを決めない(頭の上・足元)
    private const float Touching = 0.3f;

    /// <summary>listener が forward を向いているとき、source の音を左右にどう振り分けるか。高さは見ない。</summary>
    public static StereoPlacement Place(Vector3 listener, Vector3 forward, Vector3 source)
    {
        var toSource = Angles.Horizontal(source - listener);
        var facing = Angles.Horizontal(forward);
        if (toSource.Length() < Touching || facing.LengthSquared() < 1e-6f)
            return StereoPlacement.Centre;

        // FFXIV の座標では、forward を向いたときの右手は (-forward.Z, 0, forward.X)
        var direction = Vector3.Normalize(toSource);
        var right = Vector3.Normalize(new Vector3(-facing.Z, 0f, facing.X));
        var side = Vector3.Dot(direction, right);
        var behind = MathF.Max(0f, -Vector3.Dot(direction, Vector3.Normalize(facing)));
        var farGain = 1f - MaxPan * MathF.Abs(side);
        var farDelay = (int)MathF.Round(MaxDelaySamples * MathF.Abs(side));
        return side >= 0f
            ? new StereoPlacement(farGain, 1f, farDelay, 0, behind)
            : new StereoPlacement(1f, farGain, 0, farDelay, behind);
    }
}
