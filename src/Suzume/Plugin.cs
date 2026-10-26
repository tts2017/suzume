using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Suzume.Windows;

namespace Suzume;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/suzume";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly Configuration config;
    private readonly WindowSystem windowSystem = new("Suzume");
    private readonly ConfigWindow configWindow;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly ProbeRunner probe = new();

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

        configWindow = new ConfigWindow(config, probe.Describe, kind => chirpPlayer?.Play(kind, config.Volume));
        windowSystem.AddWindow(configWindow);
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        Framework.Update += probe.OnUpdate;
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = "設定画面を開きます。/suzume probe <bounce|offsetx|spin|pitch|pitchdraw|anim <id>|play <id>|freeze|off> / /suzume info はデバッグ用です。",
        });
    }

    private void OnDraw()
    {
        probe.OnDraw();
        windowSystem.Draw();
    }

    private void HandleCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            configWindow.Toggle();
            return;
        }

        if (parts.Length >= 2 && parts[0] == "probe")
        {
            var id = parts.Length >= 3 && ushort.TryParse(parts[2], out var value) ? value : (ushort)0;
            probe.Set(parts[1], id);
            ChatGui.Print($"[Suzume] probe = {parts[1]} {id}");
            return;
        }

        ChatGui.Print($"[Suzume] {probe.Describe()}");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        Framework.Update -= probe.OnUpdate;
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();
        probe.Release();
        chirpPlayer?.Dispose();
    }
}
