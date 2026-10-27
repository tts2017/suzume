using System.Numerics;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using Suzume.Core;

namespace Suzume;

/// <summary>
/// タビスズメのボーンを直接動かす。ゲームのアニメ更新 (Skeleton.UpdateAnimations) の直後に書き込む
/// (更新処理や描画直前で書いても、アニメに上書きされて描画に残らないことをスパイクで確認した)。
/// </summary>
public sealed unsafe class BonePoser : IDisposable
{
    // 左右の翼の付け根と、その先のボーン
    private static readonly (string Root, string Tip)[] Wings = [("j_ude_a_l", "j_ude_b_l"), ("j_ude_a_r", "j_ude_b_r")];

    private readonly Hook<Skeleton.Delegates.UpdateAnimations>? updateHook;
    private readonly int[] rootIndex = [-1, -1];
    private readonly int[] tipIndex = [-1, -1];
    private hkaSkeleton* indexedSkeleton;

    public BonePoser()
    {
        var update = (nint)Skeleton.Addresses.UpdateAnimations.Value;
        if (update == 0)
        {
            Plugin.Log.Warning("Skeleton.UpdateAnimations が見つからないため、翼は動かしません");
            return;
        }

        updateHook = Plugin.Interop.HookFromAddress<Skeleton.Delegates.UpdateAnimations>(update, UpdateAnimationsDetour);
        updateHook.Enable();
    }

    /// <summary>翼を体の外側へ開く角度 (ラジアン)。0 ならアニメのまま。毎フレーム制御側が入れる。</summary>
    public float WingSpread { get; set; }

    /// <summary>開いた翼を上下に振る角度 (ラジアン、正で上)。</summary>
    public float WingFlap { get; set; }

    private void UpdateAnimationsDetour(Skeleton* first, float deltaTime)
    {
        updateHook!.Original(first, deltaTime);
        if (WingSpread == 0f && WingFlap == 0f)
            return;

        try
        {
            ApplyWings(WingSpread, WingFlap);
        }
        catch (Exception ex)
        {
            WingSpread = 0f;
            WingFlap = 0f;
            Plugin.Log.Error(ex, "翼のボーンの書き込みに失敗しました");
        }
    }

    private void ApplyWings(float spread, float flap)
    {
        var minion = MinionTracker.FindSparrow(out _);
        if (minion == null)
            return;

        var draw = ((GameObject*)minion)->DrawObject;
        if (draw == null || draw->Object.GetObjectType() != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
            return;

        var skeleton = ((CharacterBase*)draw)->Skeleton;
        if (skeleton == null || skeleton->PartialSkeletonCount == 0)
            return;

        var pose = skeleton->PartialSkeletons[0].GetHavokPose(0);
        if (pose == null || pose->Skeleton == null)
            return;

        IndexWings(pose->Skeleton);

        // 先に両翼の新しい局所回転をアニメ計算後のモデル空間の形から求め、まとめて書き込む
        var model = pose->AccessSyncedPoseModelSpace();
        var rotations = new Quaternion?[Wings.Length];
        for (var i = 0; i < Wings.Length; i++)
        {
            var root = rootIndex[i];
            var tip = tipIndex[i];
            if (root < 0 || tip < 0)
                continue;

            var parent = pose->Skeleton->ParentIndices[root];
            var parentRotation = parent >= 0 ? ToQuaternion(model->Data[parent].Rotation) : Quaternion.Identity;
            var shoulder = ToVector(model->Data[root].Translation);
            var toTip = Vector3.Subtract(ToVector(model->Data[tip].Translation), shoulder);
            rotations[i] = WingPose.LocalRotation(parentRotation, ToQuaternion(model->Data[root].Rotation), shoulder, toTip, spread, flap);
        }

        for (var i = 0; i < Wings.Length; i++)
        {
            if (rotations[i] is not { } rotation)
                continue;

            var local = pose->AccessBoneLocalSpace(rootIndex[i]);
            local->Rotation.X = rotation.X;
            local->Rotation.Y = rotation.Y;
            local->Rotation.Z = rotation.Z;
            local->Rotation.W = rotation.W;
        }

        // 書き換えた局所の回転から、翼の先を含めてモデル空間を計算し直す
        pose->AccessSyncedPoseModelSpace();
    }

    private static Quaternion ToQuaternion(FFXIVClientStructs.Havok.Common.Base.Math.Quaternion.hkQuaternionf q) => new(q.X, q.Y, q.Z, q.W);

    private static Vector3 ToVector(FFXIVClientStructs.Havok.Common.Base.Math.Vector.hkVector4f v) => new(v.X, v.Y, v.Z);

    private void IndexWings(hkaSkeleton* skeleton)
    {
        if (skeleton == indexedSkeleton)
            return;

        for (var i = 0; i < Wings.Length; i++)
        {
            rootIndex[i] = -1;
            tipIndex[i] = -1;
        }

        for (var bone = 0; bone < skeleton->Bones.Length; bone++)
        {
            var name = skeleton->Bones[bone].Name.String;
            for (var i = 0; i < Wings.Length; i++)
            {
                if (name == Wings[i].Root)
                    rootIndex[i] = bone;
                else if (name == Wings[i].Tip)
                    tipIndex[i] = bone;
            }
        }

        indexedSkeleton = skeleton;
        Plugin.Log.Debug($"翼のボーン: 左 {rootIndex[0]}→{tipIndex[0]} 右 {rootIndex[1]}→{tipIndex[1]}");
    }

    public void Dispose() => updateHook?.Dispose();
}
