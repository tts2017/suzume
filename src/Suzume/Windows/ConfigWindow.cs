using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Suzume.Core;

namespace Suzume.Windows;

public sealed class ConfigWindow : Window
{
    // 言語が変わってもウィンドウの位置と大きさを引き継ぐための ID
    private const string WindowId = "###SuzumeConfig";

    private readonly Configuration config;
    private readonly Func<string> debugText;
    private readonly Action<ChirpKind> preview;
    private readonly Action resetPosition;

    public ConfigWindow(Configuration config, Func<string> debugText, Action<ChirpKind> preview, Action resetPosition)
        : base($"{Plugin.Text.WindowTitle}{WindowId}")
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

    private static UiText Text => Plugin.Text;

    public override void PreDraw() => WindowName = $"{Text.WindowTitle}{WindowId}";

    public override void Draw()
    {
        var changed = false;

        var enabled = config.Enabled;
        if (ImGui.Checkbox(Text.Enabled, ref enabled))
        {
            config.Enabled = enabled;
            changed = true;
        }

        var includeOthers = config.IncludeOthers;
        if (ImGui.Checkbox(Text.IncludeOthers, ref includeOthers))
        {
            config.IncludeOthers = includeOthers;
            changed = true;
        }

        var sound = config.SoundEnabled;
        if (ImGui.Checkbox(Text.Sound, ref sound))
        {
            config.SoundEnabled = sound;
            changed = true;
        }

        var stereo = config.StereoSound;
        if (ImGui.Checkbox(Text.StereoSound, ref stereo))
        {
            config.StereoSound = stereo;
            changed = true;
        }

        var volume = config.Volume;
        if (ImGui.SliderFloat(Text.Volume, ref volume, 0f, 1f))
        {
            config.Volume = volume;
            changed = true;
        }

        var frequency = config.ChirpFrequency;
        if (ImGui.SliderFloat(Text.ChirpFrequency, ref frequency, 0f, 3f))
        {
            config.ChirpFrequency = frequency;
            changed = true;
        }

        var flyDistance = config.FlyDistance;
        if (ImGui.SliderFloat(Text.FlyDistance, ref flyDistance, 2f, 20f))
        {
            config.FlyDistance = flyDistance;
            changed = true;
        }

        var wanderDistance = config.WanderDistance;
        if (ImGui.SliderFloat(Text.WanderDistance, ref wanderDistance, 1f, 10f))
        {
            config.WanderDistance = wanderDistance;
            changed = true;
        }

        var headOffset = config.HeadOffset;
        if (ImGui.SliderFloat(Text.HeadOffset, ref headOffset, -1f, 1f))
        {
            config.HeadOffset = headOffset;
            changed = true;
        }

        var puffed = config.Puffed;
        if (ImGui.Checkbox(Text.Puffed, ref puffed))
        {
            config.Puffed = puffed;
            changed = true;
        }

        if (config.Puffed)
        {
            var puffAmount = config.PuffAmount * 100f;
            if (ImGui.SliderFloat(Text.PuffAmount, ref puffAmount, 5f, 30f, "%.0f"))
            {
                config.PuffAmount = puffAmount / 100f;
                changed = true;
            }
        }

        ImGui.TextDisabled(Text.BeckonHint);
        ImGui.TextDisabled(Text.ScareHint);

        if (ImGui.Button(Text.ResetPosition))
            resetPosition();

        ImGui.Separator();
        ImGui.TextUnformatted(Text.Preview);
        foreach (var kind in Enum.GetValues<ChirpKind>())
        {
            ImGui.SameLine();
            if (ImGui.Button(Text.ChirpLabel(kind)))
                preview(kind);
        }

        var showDebug = config.ShowDebug;
        if (ImGui.Checkbox(Text.ShowDebug, ref showDebug))
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
}
