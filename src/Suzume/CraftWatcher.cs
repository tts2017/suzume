using FFXIVClientStructs.FFXIV.Component.GUI;
using Suzume.Core;

namespace Suzume;

/// <summary>
/// 自分の通常制作が完成した瞬間と、簡易制作中かどうかを見る。
/// 通常制作はウィンドウ (Synthesis) の工数を読み、簡易制作は別のウィンドウ (SynthesisSimple) が開いているかで見る。
/// </summary>
public sealed unsafe class CraftWatcher
{
    // Synthesis の AtkValues で、今の工数と完成に必要な工数が入っている位置
    private const int ProgressIndex = 5;
    private const int MaxProgressIndex = 6;

    private readonly CraftCompletion completion = new();
    private bool wasOpen;
    private (int Progress, int Max) lastLogged;

    /// <summary>簡易制作のウィンドウが開いている (Update で更新)。</summary>
    public bool QuickSynthesis { get; private set; }

    /// <returns>このフレームで制作が完成した</returns>
    public bool Update()
    {
        var simple = Addon("SynthesisSimple");
        var quick = simple != null && simple->IsVisible;
        if (quick != QuickSynthesis)
            Plugin.Log.Debug($"簡易制作 {QuickSynthesis} -> {quick}");
        QuickSynthesis = quick;

        var addon = Addon("Synthesis");
        var open = addon != null && addon->IsVisible;
        var progress = open ? IntValue(addon, ProgressIndex) : 0;
        var max = open ? IntValue(addon, MaxProgressIndex) : 0;

        if (open && !wasOpen)
            LogValues(addon);
        if (open && (progress, max) != lastLogged)
        {
            Plugin.Log.Debug($"制作の工数 {progress}/{max}");
            lastLogged = (progress, max);
        }
        if (!open)
            lastLogged = default;
        wasOpen = open;

        var completed = completion.Update(open, progress, max);
        if (completed)
            Plugin.Log.Debug($"制作が完成しました (工数 {progress}/{max})");
        return completed;
    }

    private static AtkUnitBase* Addon(string name)
    {
        var stage = AtkStage.Instance();
        if (stage == null || stage->RaptureAtkUnitManager == null)
            return null;
        return stage->RaptureAtkUnitManager->GetAddonByName(name);
    }

    private static int IntValue(AtkUnitBase* addon, int index)
    {
        if (addon->AtkValues == null || index >= addon->AtkValuesCount)
            return 0;
        var value = addon->AtkValues[index];
        return value.Type switch
        {
            AtkValueType.Int => value.Int,
            AtkValueType.UInt => (int)value.UInt,
            _ => 0,
        };
    }

    /// <summary>確かめ用: ウィンドウが開いたときの AtkValues の数値を並べて記録する。</summary>
    private static void LogValues(AtkUnitBase* addon)
    {
        var count = Math.Min((int)addon->AtkValuesCount, 24);
        var values = Enumerable.Range(0, count).Select(i => $"{i}={IntValue(addon, i)}");
        Plugin.Log.Debug($"制作のウィンドウが開きました: {string.Join(" ", values)}");
    }
}
