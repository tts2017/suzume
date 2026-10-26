using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

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

    private readonly ProbeRunner probe = new();

    public Plugin()
    {
        Framework.Update += probe.OnUpdate;
        PluginInterface.UiBuilder.Draw += probe.OnDraw;
        CommandManager.AddHandler(CommandName, new CommandInfo((_, args) => HandleCommand(args))
        {
            HelpMessage = "/suzume probe <off|bounce|offsetx|spin|pitch|pitchdraw|anim <id>|freeze> / /suzume info",
        });
    }

    private void HandleCommand(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
        PluginInterface.UiBuilder.Draw -= probe.OnDraw;
        probe.Release();
    }
}
