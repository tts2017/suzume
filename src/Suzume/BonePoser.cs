using System.Numerics;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.Quaternion;
using FFXIVClientStructs.Havok.Common.Base.Math.Vector;
using Suzume.Core;

namespace Suzume;

/// <summary>
/// タビスズメ (複数羽) のボーン (翼・首・尾) を直接動かす。ゲームのアニメ更新 (Skeleton.UpdateAnimations) の直後に書き込む
/// (更新処理や描画直前で書いても、アニメに上書きされて描画に残らないことをスパイクで確認した)。
/// </summary>
public sealed unsafe class BonePoser : IDisposable
{
    private const string LeftWing = "j_ude_a_l";
    private const string LeftWingTip = "j_ude_b_l";
    private const string RightWing = "j_ude_a_r";
    private const string RightWingTip = "j_ude_b_r";
    private const string Neck = "j_kubi";
    private const string Tail = "j_tail";
    private static readonly string[] BoneNames = [LeftWing, LeftWingTip, RightWing, RightWingTip, Neck, Tail];

    private readonly Hook<Skeleton.Delegates.UpdateAnimations>? updateHook;
    // オブジェクト番号ごとの、書き込むポーズ (書き込む直前に同じスズメかを確かめる)
    private readonly Dictionary<ushort, (nint Minion, SparrowPose Pose)> poses = new();
    private readonly object gate = new();
    // 骨格ごとのボーン番号 (タビスズメはどれも同じ骨格なので、たいてい 1 つ)
    private readonly Dictionary<nint, Dictionary<string, int>> boneIndexBySkeleton = new();
    // 骨格ごとの、ふくら雀の倍率 (ふくらませ具合が変わったら作り直す)
    private readonly Dictionary<nint, (float Amount, float[] Factors)> puffBySkeleton = new();

    public BonePoser()
    {
        var update = (nint)Skeleton.Addresses.UpdateAnimations.Value;
        if (update == 0)
        {
            Plugin.Log.Warning("Skeleton.UpdateAnimations が見つからないため、ボーンは動かしません");
            return;
        }

        updateHook = Plugin.Interop.HookFromAddress<Skeleton.Delegates.UpdateAnimations>(update, UpdateAnimationsDetour);
        updateHook.Enable();
    }

    /// <summary>ふくら雀: 体をどれだけ大きくするか (0.15 で 15%)。0 で元の形。</summary>
    public float Puff { get; set; }

    /// <summary>毎フレーム制御側が入れる。すべて 0 ならアニメのまま。</summary>
    public void SetPose(ushort objectIndex, nint minion, in SparrowPose pose)
    {
        lock (gate)
            poses[objectIndex] = (minion, pose);
    }

    public void Remove(ushort objectIndex)
    {
        lock (gate)
            poses.Remove(objectIndex);
    }

    /// <summary>もうそこにいないスズメのポーズを捨てる。</summary>
    public void RemoveMissing()
    {
        lock (gate)
        {
            foreach (var index in poses.Keys.Where(index => !IsSameObject(index, poses[index].Minion)).ToList())
                poses.Remove(index);
        }
    }

    public void Clear()
    {
        lock (gate)
            poses.Clear();
    }

    private static bool IsSameObject(ushort objectIndex, nint minion) =>
        Plugin.ObjectTable[objectIndex] is { } obj && obj.Address == minion;

    private bool HasBoneWork(in SparrowPose pose) =>
        Puff > 0f || pose.WingSpread != 0f || pose.WingFlap != 0f || pose.NeckYaw != 0f || pose.NeckPitch != 0f || pose.NeckRoll != 0f || pose.TailLift != 0f;

    private void UpdateAnimationsDetour(Skeleton* first, float deltaTime)
    {
        updateHook!.Original(first, deltaTime);

        (ushort Index, nint Minion, SparrowPose Pose)[] work;
        lock (gate)
            work = poses.Where(x => HasBoneWork(x.Value.Pose)).Select(x => (x.Key, x.Value.Minion, x.Value.Pose)).ToArray();

        foreach (var (index, minion, pose) in work)
        {
            try
            {
                // 消えたスズメのメモリには触らない
                if (IsSameObject(index, minion))
                    Apply((GameObject*)minion, pose);
            }
            catch (Exception ex)
            {
                Remove(index);
                Plugin.Log.Error(ex, "ボーンの書き込みに失敗しました");
            }
        }
    }

    private void Apply(GameObject* minion, in SparrowPose sparrow)
    {
        var draw = minion->DrawObject;
        if (draw == null || draw->Object.GetObjectType() != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
            return;

        var skeleton = ((CharacterBase*)draw)->Skeleton;
        if (skeleton == null || skeleton->PartialSkeletonCount == 0)
            return;

        var pose = skeleton->PartialSkeletons[0].GetHavokPose(0);
        if (pose == null || pose->Skeleton == null)
            return;

        var boneIndex = IndexBones(pose->Skeleton);

        // 先にアニメ計算後のモデル空間の形から新しい局所回転をすべて求め、あとでまとめて書き込む
        var model = pose->AccessSyncedPoseModelSpace();
        var writes = new List<(int Bone, Quaternion Rotation)>(4);

        if (sparrow.WingSpread != 0f || sparrow.WingFlap != 0f)
        {
            // WingSide: 0 は両方、負は左だけ、正は右だけ
            if (sparrow.WingSide <= 0f)
                AddWing(pose, model, boneIndex, LeftWing, LeftWingTip, sparrow, writes);
            if (sparrow.WingSide >= 0f)
                AddWing(pose, model, boneIndex, RightWing, RightWingTip, sparrow, writes);
        }

        if ((sparrow.NeckYaw != 0f || sparrow.NeckPitch != 0f || sparrow.NeckRoll != 0f) && boneIndex.TryGetValue(Neck, out var neck))
            writes.Add((neck, BoneTwist.LocalRotation(ParentRotation(pose, model, neck), Rotation(model, neck), sparrow.NeckYaw, sparrow.NeckPitch, sparrow.NeckRoll)));

        if (sparrow.TailLift != 0f && boneIndex.TryGetValue(Tail, out var tail))
            writes.Add((tail, BoneTwist.LocalRotation(ParentRotation(pose, model, tail), Rotation(model, tail), 0f, sparrow.TailLift, 0f)));

        foreach (var (bone, rotation) in writes)
        {
            var local = pose->AccessBoneLocalSpace(bone);
            local->Rotation.X = rotation.X;
            local->Rotation.Y = rotation.Y;
            local->Rotation.Z = rotation.Z;
            local->Rotation.W = rotation.W;
        }

        if (Puff > 0f)
            ApplyPuff(pose);

        // 書き換えた局所の回転から、子のボーン (翼の先・頭・尾の先) を含めてモデル空間を計算し直す
        pose->AccessSyncedPoseModelSpace();
    }

    /// <summary>ふくら雀: 体のボーンを大きくし、子では割り戻して くちばし・翼・尾・脚を元の大きさに保つ。</summary>
    private void ApplyPuff(hkaPose* pose)
    {
        var skeleton = pose->Skeleton;
        if (!puffBySkeleton.TryGetValue((nint)skeleton, out var cached) || cached.Amount != Puff)
        {
            var count = skeleton->Bones.Length;
            var names = new string?[count];
            var parents = new int[count];
            for (var bone = 0; bone < count; bone++)
            {
                names[bone] = skeleton->Bones[bone].Name.String;
                parents[bone] = skeleton->ParentIndices[bone];
            }

            cached = (Puff, PuffScale.LocalFactors(names, parents, Puff));
            puffBySkeleton[(nint)skeleton] = cached;
            Plugin.Log.Debug("ふくら雀の骨格: " + string.Join(", ", Enumerable.Range(0, count)
                .Select(b => $"{names[b]}<{(parents[b] >= 0 ? names[parents[b]] : "-")} x{cached.Factors[b]:F3}")));
        }

        for (var bone = 0; bone < cached.Factors.Length; bone++)
        {
            var factor = cached.Factors[bone];
            if (factor == 1f)
                continue;

            // アニメの大きさは 1 なので、掛け算でなく代入する (毎フレーム積み重なって大きくなり続けないように)
            var local = pose->AccessBoneLocalSpace(bone);
            local->Scale.X = factor;
            local->Scale.Y = factor;
            local->Scale.Z = factor;
        }
    }

    private static void AddWing(hkaPose* pose, FFXIVClientStructs.Havok.Common.Base.Container.Array.hkArray<FFXIVClientStructs.Havok.Common.Base.Math.QsTransform.hkQsTransformf>* model,
        Dictionary<string, int> boneIndex, string root, string tip, in SparrowPose sparrow, List<(int Bone, Quaternion Rotation)> writes)
    {
        if (!boneIndex.TryGetValue(root, out var rootIndex) || !boneIndex.TryGetValue(tip, out var tipIndex))
            return;

        var shoulder = ToVector(model->Data[rootIndex].Translation);
        var toTip = Vector3.Subtract(ToVector(model->Data[tipIndex].Translation), shoulder);
        writes.Add((rootIndex, WingPose.LocalRotation(
            ParentRotation(pose, model, rootIndex), Rotation(model, rootIndex), shoulder, toTip, sparrow.WingSpread, sparrow.WingFlap)));
    }

    private static Quaternion ParentRotation(hkaPose* pose, FFXIVClientStructs.Havok.Common.Base.Container.Array.hkArray<FFXIVClientStructs.Havok.Common.Base.Math.QsTransform.hkQsTransformf>* model, int bone)
    {
        var parent = pose->Skeleton->ParentIndices[bone];
        return parent >= 0 ? Rotation(model, parent) : Quaternion.Identity;
    }

    private static Quaternion Rotation(FFXIVClientStructs.Havok.Common.Base.Container.Array.hkArray<FFXIVClientStructs.Havok.Common.Base.Math.QsTransform.hkQsTransformf>* model, int bone) =>
        ToQuaternion(model->Data[bone].Rotation);

    private static Quaternion ToQuaternion(hkQuaternionf q) => new(q.X, q.Y, q.Z, q.W);

    private static Vector3 ToVector(hkVector4f v) => new(v.X, v.Y, v.Z);

    private Dictionary<string, int> IndexBones(hkaSkeleton* skeleton)
    {
        if (boneIndexBySkeleton.TryGetValue((nint)skeleton, out var known))
            return known;

        var boneIndex = new Dictionary<string, int>();
        for (var bone = 0; bone < skeleton->Bones.Length; bone++)
        {
            var name = skeleton->Bones[bone].Name.String;
            if (name != null && BoneNames.Contains(name))
                boneIndex[name] = bone;
        }

        boneIndexBySkeleton[(nint)skeleton] = boneIndex;
        Plugin.Log.Debug($"ボーン: {string.Join(", ", boneIndex.Select(x => $"{x.Key}={x.Value}"))}");
        return boneIndex;
    }

    public void Dispose() => updateHook?.Dispose();
}
