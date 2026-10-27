using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

namespace Suzume;

/// <summary>
/// キャラクターの骨格から名前で指定したボーンの位置を読む。見つけた場所はキャッシュし、毎フレーム名前を比べない。
/// </summary>
public sealed unsafe class BoneLocator(string boneName)
{
    private hkaSkeleton* cachedSkeleton;
    private int cachedPartial = -1;
    private int cachedBone = -1;
    // 調査用: 見つからない理由を 1 回だけログに出す
    private bool reportedFailure;

    /// <summary>
    /// ボーンの位置。<paramref name="modelOffset"/> はボーンからのずれ(モデル空間、スケール前)。
    /// 骨格が読めなければ null。
    /// </summary>
    public Vector3? WorldPosition(GameObject* obj, Vector3 modelOffset = default)
    {
        if (!TryGetSkeleton(obj, out var skeleton) || !TryGetPose(skeleton, out var pose))
            return null;

        var bone = pose->ModelPose.Data[cachedBone].Translation;
        var transform = skeleton->Transform;
        var model = Vector3.Multiply(Vector3.Add(new Vector3(bone.X, bone.Y, bone.Z), modelOffset), transform.Scale);
        return Vector3.Add(transform.Position, Vector3.Transform(model, transform.Rotation));
    }

    /// <summary>モデル原点(足元)から見たボーンの高さ(スケール込み)。骨格が読めなければ null。</summary>
    public float? HeightAboveOrigin(GameObject* obj)
    {
        if (!TryGetSkeleton(obj, out var skeleton) || !TryGetPose(skeleton, out var pose))
            return null;

        return pose->ModelPose.Data[cachedBone].Translation.Y * skeleton->Transform.Scale.Y;
    }

    private bool TryGetSkeleton(GameObject* obj, out Skeleton* skeleton)
    {
        skeleton = null;
        var drawObject = obj->DrawObject;
        if (drawObject == null)
            return Fail("DrawObject なし");
        var type = drawObject->Object.GetObjectType();
        if (type != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
            return Fail($"DrawObject の種類 {type}");

        skeleton = ((CharacterBase*)drawObject)->Skeleton;
        if (skeleton == null || skeleton->PartialSkeletons == null)
            return Fail("Skeleton なし");

        return true;
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
                if (bones[i].Name.String != boneName)
                    continue;

                cachedSkeleton = pose->Skeleton;
                cachedPartial = partial;
                cachedBone = i;
                Plugin.Log.Debug($"ボーン {boneName} を partial {partial} の {i} 番で見つけました");
                return true;
            }
        }

        cachedPartial = -1;
        pose = null;
        return Fail($"ボーン {boneName} がありません ({skeleton->PartialSkeletonCount} partials)");
    }

    private bool Fail(string reason)
    {
        if (!reportedFailure)
        {
            reportedFailure = true;
            Plugin.Log.Debug($"ボーン {boneName} が読めません: {reason}");
        }

        return false;
    }
}
