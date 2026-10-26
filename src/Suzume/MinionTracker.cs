using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Suzume;

/// <summary>ローカルプレイヤーが呼び出しているタビスズメを探す。</summary>
public static unsafe class MinionTracker
{
    public const ushort SparrowCompanionId = 247;

    /// <summary>タビスズメが出ていなければ null。owner はプレイヤーがいれば常に設定する。</summary>
    public static Companion* FindSparrow(out Character* owner)
    {
        owner = null;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
            return null;

        owner = (Character*)player.Address;
        if (owner->CompanionData.CompanionId != SparrowCompanionId)
            return null;

        return owner->CompanionData.CompanionObject;
    }
}
