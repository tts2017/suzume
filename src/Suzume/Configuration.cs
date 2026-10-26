using Dalamud.Configuration;

namespace Suzume;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public float Volume { get; set; } = 0.6f;
    public float ChirpFrequency { get; set; } = 1.0f;
    public float FlyDistance { get; set; } = 6f;
    public bool ShowDebug { get; set; }

    public void Normalize()
    {
        Volume = Math.Clamp(Volume, 0f, 1f);
        ChirpFrequency = Math.Clamp(ChirpFrequency, 0f, 3f);
        FlyDistance = Math.Clamp(FlyDistance, 2f, 20f);
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
