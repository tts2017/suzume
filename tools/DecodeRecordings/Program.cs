// 録音 (mp3 / m4a / wav) を 44.1kHz・16bit・モノラルの WAV に変換する。
// 使い方: dotnet run --project tools/DecodeRecordings -- <入力フォルダ> <出力フォルダ>
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

const int Rate = 44100;

if (args.Length != 2)
{
    Console.Error.WriteLine("使い方: DecodeRecordings <入力フォルダ> <出力フォルダ>");
    return 1;
}

Directory.CreateDirectory(args[1]);
foreach (var path in Directory.GetFiles(args[0]).Where(p => p.EndsWith(".mp3") || p.EndsWith(".m4a") || p.EndsWith(".wav")).Order())
{
    using var reader = new MediaFoundationReader(path);
    ISampleProvider samples = reader.ToSampleProvider();
    if (samples.WaveFormat.Channels == 2)
        samples = new StereoToMonoSampleProvider(samples);
    if (samples.WaveFormat.SampleRate != Rate)
        samples = new WdlResamplingSampleProvider(samples, Rate);

    var output = Path.Combine(args[1], Path.GetFileNameWithoutExtension(path) + ".wav");
    WaveFileWriter.CreateWaveFile16(output, samples);
    Console.WriteLine($"{Path.GetFileName(path)} -> {output} ({reader.TotalTime.TotalSeconds:F1}s)");
}

return 0;
