using Dalamud.Configuration;

namespace Suzume;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    // 他の人のタビスズメも、あなたの画面の中でリアルに動かす
    public bool IncludeOthers { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public float Volume { get; set; } = 0.6f;
    public float ChirpFrequency { get; set; } = 1.0f;
    public float FlyDistance { get; set; } = 6f;
    // 頭に乗るときの高さの補正 (yalm)。キャラクターの身長に合わせて調整する
    public float HeadOffset { get; set; }
    public bool ShowDebug { get; set; }

    public void Normalize()
    {
        Volume = Math.Clamp(Volume, 0f, 1f);
        ChirpFrequency = Math.Clamp(ChirpFrequency, 0f, 3f);
        FlyDistance = Math.Clamp(FlyDistance, 2f, 20f);
        HeadOffset = Math.Clamp(HeadOffset, -1f, 1f);
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
