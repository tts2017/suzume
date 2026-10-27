using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Suzume.Core;

namespace Suzume.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Configuration config;
    private readonly Func<string> debugText;
    private readonly Action<ChirpKind> preview;
    private readonly Action resetPosition;

    public ConfigWindow(Configuration config, Func<string> debugText, Action<ChirpKind> preview, Action resetPosition)
        : base("タビスズメ###SuzumeConfig")
    {
        this.config = config;
        this.debugText = debugText;
        this.preview = preview;
        this.resetPosition = resetPosition;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 260),
            MaximumSize = new Vector2(800, 900),
        };
    }

    public override void Draw()
    {
        var changed = false;

        var enabled = config.Enabled;
        if (ImGui.Checkbox("有効", ref enabled))
        {
            config.Enabled = enabled;
            changed = true;
        }

        var sound = config.SoundEnabled;
        if (ImGui.Checkbox("鳴き声", ref sound))
        {
            config.SoundEnabled = sound;
            changed = true;
        }

        var volume = config.Volume;
        if (ImGui.SliderFloat("音量", ref volume, 0f, 1f))
        {
            config.Volume = volume;
            changed = true;
        }

        var frequency = config.ChirpFrequency;
        if (ImGui.SliderFloat("鳴く頻度", ref frequency, 0f, 3f))
        {
            config.ChirpFrequency = frequency;
            changed = true;
        }

        var flyDistance = config.FlyDistance;
        if (ImGui.SliderFloat("飛ぶ距離 (yalm)", ref flyDistance, 2f, 20f))
        {
            config.FlyDistance = flyDistance;
            changed = true;
        }

        var headOffset = config.HeadOffset;
        if (ImGui.SliderFloat("頭の高さの補正 (yalm)", ref headOffset, -1f, 1f))
        {
            config.HeadOffset = headOffset;
            changed = true;
        }

        ImGui.TextDisabled("/beckon (手まねき) で頭に乗り、もう一度で下ります");
        ImGui.TextDisabled("スズメに向けて /angry /furious /slap /battlestance /box /stomp /throw をすると逃げます");

        if (ImGui.Button("スズメをそばに置き直す"))
            resetPosition();

        ImGui.Separator();
        ImGui.TextUnformatted("試聴");
        foreach (var kind in Enum.GetValues<ChirpKind>())
        {
            ImGui.SameLine();
            if (ImGui.Button(Label(kind)))
                preview(kind);
        }

        var showDebug = config.ShowDebug;
        if (ImGui.Checkbox("デバッグ表示", ref showDebug))
        {
            config.ShowDebug = showDebug;
            changed = true;
        }

        if (config.ShowDebug)
            ImGui.TextUnformatted(debugText());

        if (changed)
        {
            config.Normalize();
            config.Save();
        }
    }

    private static string Label(ChirpKind kind) => kind switch
    {
        ChirpKind.Contact => "チュン",
        ChirpKind.Song => "チュンチュン",
        ChirpKind.Alarm => "ジュクジュク",
        ChirpKind.Flight => "チュリリ",
        ChirpKind.Dust => "バサバサ",
        _ => kind.ToString(),
    };
}
