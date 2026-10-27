using System.Reflection;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Suzume.Core;

namespace Suzume;

/// <summary>埋め込みの WAV (モノラル) を NAudio で左右に振り分けて鳴らす。同時再生は最大 2 つ。</summary>
public sealed class ChirpPlayer : IDisposable
{
    private const int MaxConcurrent = 2;
    private static readonly WaveFormat ClipFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
    private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    private readonly Dictionary<ChirpKind, float[][]> clips = new();
    private readonly MixingSampleProvider mixer = new(Format) { ReadFully = true };
    // ゲームの負荷で出力スレッドが遅れても途切れない(プチッと鳴らない)よう、バッファを厚めにする
    private readonly WaveOutEvent output = new() { DesiredLatency = 200, NumberOfBuffers = 3 };
    private readonly Random random = new();

    public ChirpPlayer()
    {
        foreach (var kind in Enum.GetValues<ChirpKind>())
            clips[kind] = LoadVariants(kind);

        output.Init(mixer);
        output.Play();
    }

    public void Play(ChirpKind kind, float volume, StereoPlacement placement)
    {
        if (volume <= 0f || mixer.MixerInputs.Count() >= MaxConcurrent)
            return;

        var variants = clips[kind];
        var clip = variants[random.Next(variants.Length)];
        mixer.AddMixerInput(new StereoClipSampleProvider(clip, volume, placement));
    }

    public void Dispose()
    {
        output.Stop();
        output.Dispose();
    }

    private static float[][] LoadVariants(ChirpKind kind)
    {
        var prefix = $"Suzume.Sounds.{kind.ToString().ToLowerInvariant()}_";
        var assembly = Assembly.GetExecutingAssembly();
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Order()
            .ToArray();
        if (names.Length == 0)
            throw new InvalidOperationException($"鳴き声のリソースがありません: {prefix}*");

        return names.Select(name => ReadSamples(assembly, name)).ToArray();
    }

    private static float[] ReadSamples(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"リソースを開けません: {name}");
        using var reader = new WaveFileReader(stream);
        var provider = reader.ToSampleProvider();
        if (provider.WaveFormat.SampleRate != ClipFormat.SampleRate || provider.WaveFormat.Channels != ClipFormat.Channels)
            throw new InvalidOperationException($"{name} は 44.1kHz モノラルではありません");

        var samples = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            samples.AddRange(buffer.Take(read));
        return samples.ToArray();
    }

    /// <summary>メモリ上のモノラル 1 クリップを、左右の耳ごとの音量と遅れを付けて最後まで流す。</summary>
    private sealed class StereoClipSampleProvider(float[] clip, float volume, StereoPlacement placement) : ISampleProvider
    {
        private readonly float leftGain = volume * placement.LeftGain;
        private readonly float rightGain = volume * placement.RightGain;
        private readonly int frames = clip.Length + Math.Max(placement.LeftDelaySamples, placement.RightDelaySamples);
        private int position;

        public WaveFormat WaveFormat => Format;

        public int Read(float[] buffer, int offset, int count)
        {
            var n = Math.Min(count / 2, frames - position);
            for (var i = 0; i < n; i++, position++)
            {
                buffer[offset + i * 2] = Sample(position - placement.LeftDelaySamples) * leftGain;
                buffer[offset + i * 2 + 1] = Sample(position - placement.RightDelaySamples) * rightGain;
            }
            return n * 2;
        }

        private float Sample(int index) =>
            index >= 0 && index < clip.Length ? clip[index] : 0f;
    }
}
