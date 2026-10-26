namespace Suzume.Core;

/// <summary>プレイヤーとの距離による音量の減衰。</summary>
public static class ChirpVolume
{
    public const float FullVolumeDistance = 5f;
    public const float SilentDistance = 30f;

    public static float Compute(float baseVolume, float distance)
    {
        var attenuation = Math.Clamp(
            (SilentDistance - distance) / (SilentDistance - FullVolumeDistance), 0f, 1f);
        return Math.Clamp(baseVolume, 0f, 1f) * attenuation;
    }
}
