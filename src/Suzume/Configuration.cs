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
    // スズメのいる向きで、左右の耳の音量と届く時間をずらす
    public bool StereoSound { get; set; } = true;
    public float ChirpFrequency { get; set; } = 1.0f;
    public float FlyDistance { get; set; } = 6f;
    // 頭に乗るときの高さの補正 (yalm)。キャラクターの身長に合わせて調整する
    public float HeadOffset { get; set; }
    // ふくら雀: 冬のスズメのように羽毛をふくらませた丸い形にする
    public bool Puffed { get; set; } = true;
    // 体をどれだけ大きくするか (0.15 で 15%)
    public float PuffAmount { get; set; } = 0.15f;
    public bool ShowDebug { get; set; }

    public void Normalize()
    {
        Volume = Math.Clamp(Volume, 0f, 1f);
        ChirpFrequency = Math.Clamp(ChirpFrequency, 0f, 3f);
        FlyDistance = Math.Clamp(FlyDistance, 2f, 20f);
        HeadOffset = Math.Clamp(HeadOffset, -1f, 1f);
        PuffAmount = Math.Clamp(PuffAmount, 0.05f, 0.3f);
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
