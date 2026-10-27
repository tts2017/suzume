using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Suzume;

/// <summary>
/// プレイヤーの骨格から頭のボーン (j_kao) の位置を読み、頭のてっぺんのワールド座標を求める。
/// 種族・身長・エモート中の頭の動きにそのまま追従する。
/// </summary>
public sealed unsafe class HeadLocator
{
    // 頭のボーンから頭頂までの高さ(骨格のスケール 1 のとき)
    private const float HeadTopAboveBone = 0.15f;

    private readonly BoneLocator head = new("j_kao");

    /// <summary>頭頂の位置。骨格が読めなければ null。</summary>
    public Vector3? Find(GameObject* player) =>
        head.WorldPosition(player, new Vector3(0f, HeadTopAboveBone, 0f));
}
