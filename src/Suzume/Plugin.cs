using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Suzume.Core;
using Suzume.Windows;

namespace Suzume;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/suzume";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    /// <summary>Dalamud の言語設定に合う文言。</summary>
    internal static UiText Text => UiText.For(PluginInterface.UiLanguage);

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("Suzume");
    private readonly ConfigWindow configWindow;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly SparrowController controller;
    private readonly ProbeRunner probe = new();
    private readonly BonePoser bonePoser = new();

    public Plugin()
    {
        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.Normalize();

        try
        {
            chirpPlayer = new ChirpPlayer();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "鳴き声の初期化に失敗しました。音なしで動作します");
        }

        controller = new SparrowController(config, chirpPlayer, bonePoser);
        configWindow = new ConfigWindow(config, DescribeState, kind => chirpPlayer?.Play(kind, config.Volume, StereoPlacement.Centre), controller.ResetPosition);
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        Framework.Update += OnUpdate;
        AddCommand();
        PluginInterface.LanguageChanged += OnLanguageChanged;
    }

    private void AddCommand() =>
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = Text.CommandHelp,
        });

    // コマンドの説明文を新しい言語で登録し直す
    private void OnLanguageChanged(string language)
    {
        CommandManager.RemoveHandler(CommandName);
        AddCommand();
    }

    private bool Probing => probe.Mode != ProbeRunner.Off;

    private void OnUpdate(IFramework framework)
    {
        // 確認コマンドの実行中は、通常の制御を止める
        if (Probing)
        {
            controller.Suspend();
            probe.OnUpdate(framework);
            return;
        }

        controller.Update(framework);
    }

    private void OnDraw()
    {
        if (Probing)
            probe.OnDraw();
        else
            controller.OnDraw();

        windowSystem.Draw();
    }

    private string DescribeState()
    {
        if (!controller.Active)
            return $"{Text.DebugStopped}\n{probe.Describe()}";

        var pose = controller.LastPose;
        var gap = Angles.HorizontalDistance(pose.Position, controller.LastTarget);
        return $"{Text.DebugActiveCount}: {controller.ActiveCount}\n"
            + $"{Text.DebugState}: {pose.State} / {Text.DebugAnim}: {pose.Anim}\n"
            + $"{Text.DebugPosition}: {pose.Position:F2}\n"
            + $"{Text.DebugTarget}: {controller.LastTarget:F2}\n"
            + $"{Text.DebugGap}: {gap:F2} y";
    }

    private void HandleCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            configWindow.Toggle();
            return;
        }

        switch (parts[0])
        {
            case "on":
            case "off":
                config.Enabled = parts[0] == "on";
                config.Save();
                ChatGui.Print($"[Suzume] {(config.Enabled ? Text.TurnedOn : Text.TurnedOff)}");
                return;
            case "reset":
                controller.ResetPosition();
                ChatGui.Print($"[Suzume] {Text.ResetDone}");
                return;
            case "preen":
                controller.RequestPreen();
                ChatGui.Print($"[Suzume] {Text.PreenQueued}");
                return;
            case "dust":
                controller.RequestDustBath();
                ChatGui.Print($"[Suzume] {Text.DustQueued}");
                return;
            case "probe" when parts.Length >= 2:
                var id = parts.Length >= 3 && ushort.TryParse(parts[2], out var value) ? value : (ushort)0;
                probe.Set(parts[1], id);
                ChatGui.Print($"[Suzume] probe = {parts[1]} {id}");
                return;
            default:
                ChatGui.Print($"[Suzume] probe={probe.Mode} {controller.Diagnose()}");
                ChatGui.Print($"[Suzume] {probe.Describe()}");
                return;
        }
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.LanguageChanged -= OnLanguageChanged;
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        probe.Release();
        controller.Dispose();
        bonePoser.Dispose();
        chirpPlayer?.Dispose();
    }
}
