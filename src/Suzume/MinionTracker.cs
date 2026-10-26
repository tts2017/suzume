using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Suzume;

/// <summary>ローカルプレイヤーが呼び出しているタビスズメを探す。</summary>
public static unsafe class MinionTracker
{
    public const ushort SparrowCompanionId = 247;

    /// <summary>タビスズメが出ていなければ null。owner はプレイヤーがいれば常に設定する。</summary>
    public static Companion* FindSparrow(out Character* owner)
    {
        var minion = FindOwnMinion(out owner);
        if (minion == null || ((GameObject*)minion)->BaseId != SparrowCompanionId)
            return null;

        return minion;
    }

    /// <summary>
    /// 種類を問わず、自分のミニオンを探す。CompanionData の値が入っていない場合があるため、
    /// Character のポインタ → CompanionData のポインタ → オブジェクトテーブルの所有者一致 の順で探す。
    /// </summary>
    public static Companion* FindOwnMinion(out Character* owner)
    {
        owner = null;
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player == null)
            return null;

        owner = (Character*)player.Address;
        var child = (GameObject*)owner->ChildObject;
        if (child != null && child->ObjectKind == ObjectKind.Companion)
            return (Companion*)child;
        if (owner->CompanionData.CompanionObject != null)
            return owner->CompanionData.CompanionObject;

        var ownerId = ((GameObject*)owner)->EntityId;
        foreach (var obj in Plugin.ObjectTable)
        {
            var go = (GameObject*)obj.Address;
            if (go != null && go->ObjectKind == ObjectKind.Companion && go->OwnerId == ownerId)
                return (Companion*)go;
        }

        return null;
    }
}
