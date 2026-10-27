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
        configWindow = new ConfigWindow(config, DescribeState, kind => chirpPlayer?.Play(kind, config.Volume), controller.ResetPosition);
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        Framework.Update += OnUpdate;
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = "設定画面を開きます。/suzume on|off で切り替え。/suzume reset で置き直し。/suzume dust で砂浴び、/suzume preen で毛づくろい。/suzume probe <モード> [id] / /suzume info はデバッグ用です。",
        });
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
            return $"停止中\n{probe.Describe()}";

        var pose = controller.LastPose;
        var gap = Angles.HorizontalDistance(pose.Position, controller.LastTarget);
        return $"状態: {pose.State} / アニメ: {pose.Anim}\n"
            + $"見た目の位置: {pose.Position:F2}\n"
            + $"目標地点: {controller.LastTarget:F2}\n"
            + $"差: {gap:F2} y";
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
                ChatGui.Print($"[Suzume] {(config.Enabled ? "有効" : "無効")}にしました");
                return;
            case "reset":
                controller.ResetPosition();
                ChatGui.Print("[Suzume] スズメをそばの地面に置き直しました");
                return;
            case "preen":
                controller.RequestPreen();
                ChatGui.Print("[Suzume] 次に落ち着いたら毛づくろいします");
                return;
            case "dust":
                controller.RequestDustBath();
                ChatGui.Print("[Suzume] 次に落ち着いたら砂浴びします");
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
