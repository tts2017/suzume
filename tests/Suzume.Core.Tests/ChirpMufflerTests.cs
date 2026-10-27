using Suzume.Core;

namespace Suzume.Core.Tests;

public class ChirpMufflerTests
{
    private const int Rate = 44100;

    private static float[] Sine(float frequency) =>
        Enumerable.Range(0, Rate / 4).Select(i => MathF.Sin(MathF.Tau * frequency * i / Rate)).ToArray();

    private static float Rms(float[] samples) =>
        MathF.Sqrt(samples.Skip(samples.Length / 2).Select(s => s * s).Average());

    [Fact]
    public void NoMuffleKeepsTheClip()
    {
        var clip = Sine(5000f);
        Assert.Same(clip, ChirpMuffler.Apply(clip, 0f));
    }

    [Fact]
    public void FullMuffleCutsTheHighChirpFrequencies()
    {
        var clip = Sine(5000f);
        Assert.True(Rms(ChirpMuffler.Apply(clip, 1f)) < Rms(clip) * 0.35f);
    }

    [Fact]
    public void FullMuffleKeepsLowFrequencies()
    {
        var clip = Sine(500f);
        Assert.True(Rms(ChirpMuffler.Apply(clip, 1f)) > Rms(clip) * 0.85f);
    }

    [Fact]
    public void HalfMuffleIsBetween()
    {
        var clip = Sine(5000f);
        var half = Rms(ChirpMuffler.Apply(clip, 0.5f));
        Assert.InRange(half, Rms(ChirpMuffler.Apply(clip, 1f)) + 0.05f, Rms(clip) - 0.05f);
    }

    [Fact]
    public void DoesNotChangeTheOriginalClip()
    {
        var clip = Sine(5000f);
        var copy = clip.ToArray();
        ChirpMuffler.Apply(clip, 1f);
        Assert.Equal(copy, clip);
    }
}
