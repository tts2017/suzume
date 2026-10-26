using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Suzume;

/// <summary>
/// ゲームへの書き込み方法を実機で確かめるための確認コマンド。
/// /suzume probe &lt;モード&gt; [timelineId] で切り替える。
/// </summary>
public sealed unsafe class ProbeRunner
{
    public const string Off = "off";

    private float time;

    public string Mode { get; private set; } = Off;
    public ushort TimelineId { get; private set; }

    public void Set(string mode, ushort timelineId)
    {
        Release();
        Mode = mode;
        TimelineId = timelineId;
        time = 0f;
    }

    public void OnUpdate(IFramework framework)
    {
        if (Mode == Off)
            return;

        var minion = MinionTracker.FindSparrow(out _);
        if (minion == null)
            return;

        time += (float)framework.UpdateDelta.TotalSeconds;
        var go = (GameObject*)minion;
        switch (Mode)
        {
            case "bounce":
                // 0.3y の高さで 1 秒に 2 回跳ねる
                go->SetDrawOffset(0f, 0.3f * MathF.Abs(MathF.Sin(time * MathF.PI * 2f)), 0f);
                break;
            case "offsetx":
                // ワールド X 方向に 1y ずらしたまま、ゆっくり回す
                go->SetDrawOffset(1f, 0f, 0f);
                go->SetRotation(WrapAngle(time));
                break;
            case "spin":
                go->SetRotation(WrapAngle(time * 2f));
                break;
            case "pitch":
                WritePitch(go, 0.5f);
                break;
            case "anim":
                ((Character*)minion)->Timeline.BaseOverride = TimelineId;
                break;
            case "play":
                // スロット 1 用のアニメ (idle_sp など) はベース上書きで出ないため、直接再生する。1 回だけ呼ぶ
                ((Character*)minion)->Timeline.TimelineSequencer.PlayTimeline(TimelineId, null);
                Mode = "played";
                break;
            case "freeze":
                minion->MoveDestination = go->Position;
                break;
        }
    }

    /// <summary>描画直前に書き込む版の確認(pitchdraw)。</summary>
    public void OnDraw()
    {
        if (Mode != "pitchdraw")
            return;

        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            WritePitch((GameObject*)minion, 0.5f);
    }

    public void Release()
    {
        var minion = MinionTracker.FindSparrow(out _);
        if (minion == null)
            return;

        ((GameObject*)minion)->SetDrawOffset(0f, 0f, 0f);
        ((Character*)minion)->Timeline.BaseOverride = 0;
    }

    public string Describe()
    {
        var minion = MinionTracker.FindSparrow(out var owner);
        if (owner == null)
            return "プレイヤーがいません";
        if (minion == null)
            return $"タビスズメが見つかりません。{DescribeSearch(owner)}";

        var go = (GameObject*)minion;
        return $"PlayerPos={((GameObject*)owner)->Position:F2} BaseId={go->BaseId} "
            + $"Pos={go->Position:F2} Rot={go->Rotation:F2} DrawOffset={go->DrawOffset:F2} "
            + $"BaseOverride={((Character*)minion)->Timeline.BaseOverride} Behavior={minion->Behavior} "
            + $"MoveDest={minion->MoveDestination:F2} DrawObject={(nint)go->DrawObject:X}";
    }

    /// <summary>ミニオン検出の各経路の値(タビスズメが見つからないときの調査用)。</summary>
    private static string DescribeSearch(Character* owner)
    {
        var own = MinionTracker.FindOwnMinion(out _);
        var ownerId = ((GameObject*)owner)->EntityId;
        var companions = new List<string>();
        foreach (var obj in Plugin.ObjectTable)
        {
            var go = (GameObject*)obj.Address;
            if (go != null && go->ObjectKind == ObjectKind.Companion)
                companions.Add($"[{go->ObjectIndex}] BaseId={go->BaseId} OwnerId={go->OwnerId:X}");
        }

        return $"CompanionData.CompanionId={owner->CompanionData.CompanionId} "
            + $"CompanionData.CompanionObject={(nint)owner->CompanionData.CompanionObject:X} "
            + $"Character.ChildObject={(nint)owner->ChildObject:X} "
            + $"自分のミニオン BaseId={(own != null ? ((GameObject*)own)->BaseId.ToString() : "なし")} "
            + $"プレイヤー EntityId={ownerId:X} "
            + $"ミニオン一覧: {(companions.Count == 0 ? "なし" : string.Join(", ", companions))}";
    }

    private static void WritePitch(GameObject* go, float pitch)
    {
        var draw = go->DrawObject;
        if (draw != null)
            draw->Object.Rotation = Quaternion.CreateFromYawPitchRoll(go->Rotation, pitch, 0f);
    }

    private static float WrapAngle(float angle) =>
        MathF.IEEERemainder(angle, MathF.Tau);
}
