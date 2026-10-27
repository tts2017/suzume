using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Suzume;

/// <summary>タビスズメを探す。自分のものだけ、または周りの全員のもの。</summary>
public static unsafe class MinionTracker
{
    public const ushort SparrowCompanionId = 247;

    /// <summary>見つけたタビスズメと、その持ち主。</summary>
    public readonly record struct Found(nint Minion, nint Owner, ushort ObjectIndex, uint OwnerId, bool IsLocal);

    /// <summary>周りのタビスズメを、持ち主が分かるものだけすべて返す。</summary>
    public static List<Found> FindAllSparrows()
    {
        var found = new List<Found>();
        var local = Plugin.ObjectTable.LocalPlayer;
        var localAddress = local?.Address ?? 0;
        var localChild = local != null ? (nint)((Character*)local.Address)->ChildObject : 0;
        foreach (var obj in Plugin.ObjectTable)
        {
            var go = (GameObject*)obj.Address;
            if (go == null || go->ObjectKind != ObjectKind.Companion || go->BaseId != SparrowCompanionId || go->DrawObject == null)
                continue;

            // 持ち主はミニオン自身が持つポインタを優先し、なければ OwnerId から探す
            var owner = (nint)((Companion*)go)->Owner;
            if (owner == 0)
                owner = Plugin.ObjectTable.SearchByEntityId(go->OwnerId)?.Address ?? 0;
            if (owner == 0)
                continue;

            var isLocal = owner == localAddress || obj.Address == localChild;
            if (isLocal)
                owner = localAddress;
            var ownerId = ((GameObject*)owner)->EntityId;
            found.Add(new Found(obj.Address, owner, go->ObjectIndex, ownerId, isLocal));
        }

        return found;
    }

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
