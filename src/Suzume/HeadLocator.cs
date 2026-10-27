using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

namespace Suzume;

/// <summary>
/// プレイヤーの骨格から頭のボーン (j_kao) の位置を読み、頭のてっぺんのワールド座標を求める。
/// 種族・身長・エモート中の頭の動きにそのまま追従する。
/// </summary>
public sealed unsafe class HeadLocator
{
    private const string HeadBoneName = "j_kao";
    // 頭のボーンから頭頂までの高さ(骨格のスケール 1 のとき)
    private const float HeadTopAboveBone = 0.15f;

    // 見つけたボーンの場所をキャッシュする(毎フレーム名前を比べない)
    private hkaSkeleton* cachedSkeleton;
    private int cachedPartial = -1;
    private int cachedBone = -1;
    // 調査用: 見つからない理由を 1 回だけログに出す
    private bool reportedFailure;

    /// <summary>頭頂の位置。骨格が読めなければ null。</summary>
    public Vector3? Find(GameObject* player)
    {
        var drawObject = player->DrawObject;
        if (drawObject == null)
            return Fail("DrawObject なし");
        var type = drawObject->Object.GetObjectType();
        if (type != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
            return Fail($"DrawObject の種類 {type}");

        var skeleton = ((CharacterBase*)drawObject)->Skeleton;
        if (skeleton == null || skeleton->PartialSkeletons == null)
            return Fail("Skeleton なし");

        if (!TryGetPose(skeleton, out var pose))
            return Fail(DescribeBones(skeleton));

        var bone = pose->ModelPose.Data[cachedBone].Translation;
        var transform = skeleton->Transform;
        var model = Vector3.Multiply(new Vector3(bone.X, bone.Y + HeadTopAboveBone, bone.Z), transform.Scale);
        return Vector3.Add(transform.Position, Vector3.Transform(model, transform.Rotation));
    }

    private Vector3? Fail(string reason)
    {
        if (!reportedFailure)
        {
            reportedFailure = true;
            Plugin.Log.Debug($"頭のボーンが読めません: {reason}");
        }

        return null;
    }

    private static string DescribeBones(Skeleton* skeleton)
    {
        var parts = new List<string>();
        for (var partial = 0; partial < skeleton->PartialSkeletonCount; partial++)
        {
            var pose = skeleton->PartialSkeletons[partial].GetHavokPose(0);
            if (pose == null || pose->Skeleton == null)
            {
                parts.Add($"partial{partial}: pose なし");
                continue;
            }

            var bones = pose->Skeleton->Bones;
            var names = new List<string>();
            for (var i = 0; i < bones.Length && names.Count < 12; i++)
            {
                var name = bones[i].Name.String ?? "";
                if (name.Contains("kao") || name.Contains("kubi") || name.Contains("kami") || i < 3)
                    names.Add($"{i}:{name}");
            }

            parts.Add($"partial{partial}: bones={bones.Length} modelPose={pose->ModelPose.Length} [{string.Join(" ", names)}]");
        }

        return $"partials={skeleton->PartialSkeletonCount} {string.Join(" / ", parts)}";
    }

    private bool TryGetPose(Skeleton* skeleton, out hkaPose* pose)
    {
        pose = null;
        if (cachedPartial >= 0 && cachedPartial < skeleton->PartialSkeletonCount)
        {
            pose = skeleton->PartialSkeletons[cachedPartial].GetHavokPose(0);
            if (pose != null && pose->Skeleton == cachedSkeleton && cachedBone < pose->ModelPose.Length)
                return true;
        }

        // キャラクターの読み直しなどで骨格が変わったら探し直す
        for (var partial = 0; partial < skeleton->PartialSkeletonCount; partial++)
        {
            pose = skeleton->PartialSkeletons[partial].GetHavokPose(0);
            if (pose == null || pose->Skeleton == null)
                continue;

            var bones = pose->Skeleton->Bones;
            for (var i = 0; i < bones.Length && i < pose->ModelPose.Length; i++)
            {
                if (bones[i].Name.String != HeadBoneName)
                    continue;

                cachedSkeleton = pose->Skeleton;
                cachedPartial = partial;
                cachedBone = i;
                Plugin.Log.Debug($"頭のボーン {HeadBoneName} を partial {partial} の {i} 番で見つけました");
                return true;
            }
        }

        cachedPartial = -1;
        pose = null;
        return false;
    }
}
