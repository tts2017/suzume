namespace Suzume.Core;

/// <summary>後ろから届く音のように、高い音を頭と耳でさえぎられたこもった音にする。</summary>
public static class ChirpMuffler
{
    private const float SampleRate = 44100f;
    // この周波数より上を削る (1 次のローパスを 2 段)
    private const float CutoffHz = 1500f;
    // 真後ろでも元の音をこれだけ残す
    private const float MaxMix = 0.8f;

    /// <summary>amount (0 で元のまま、1 で真後ろ) だけこもらせた新しいクリップを返す。</summary>
    public static float[] Apply(float[] clip, float amount)
    {
        var mix = Math.Clamp(amount, 0f, 1f) * MaxMix;
        if (mix <= 0f)
            return clip;

        var alpha = 1f - MathF.Exp(-MathF.Tau * CutoffHz / SampleRate);
        var result = new float[clip.Length];
        float stage1 = 0f, stage2 = 0f;
        for (var i = 0; i < clip.Length; i++)
        {
            stage1 += alpha * (clip[i] - stage1);
            stage2 += alpha * (stage1 - stage2);
            result[i] = clip[i] * (1f - mix) + stage2 * mix;
        }
        return result;
    }
}
