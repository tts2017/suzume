using System.Numerics;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

namespace Suzume;

/// <summary>
/// タビスズメのボーンを直接動かす。ゲームのアニメ更新 (Skeleton.UpdateAnimations) の直後に書き込む
/// (更新処理や描画直前で書いても、アニメに上書きされて描画に残らないことをスパイクで確認した)。
/// </summary>
public sealed unsafe class BonePoser : IDisposable
{
    // 左右の翼。同じ角度を左右対称に足す
    private static readonly (string Name, float Side)[] Wings = [("j_ude_a_l", 1f), ("j_ude_a_r", -1f)];
    // 翼を開く向き。体の内側へ閉じてしまうなら -1 にする
    private static readonly float WingOpenSign = 1f;

    private readonly Hook<Skeleton.Delegates.UpdateAnimations>? updateHook;
    private readonly int[] wingIndex = [-1, -1];
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

    /// <summary>翼のボーンに足す回転 (ラジアン)。0 ならアニメのまま。毎フレーム制御側が入れる。</summary>
    public float WingAngle { get; set; }

    private void UpdateAnimationsDetour(Skeleton* first, float deltaTime)
    {
        updateHook!.Original(first, deltaTime);
        if (WingAngle == 0f)
            return;

        try
        {
            ApplyWings(WingAngle);
        }
        catch (Exception ex)
        {
            WingAngle = 0f;
            Plugin.Log.Error(ex, "翼のボーンの書き込みに失敗しました");
        }
    }

    private void ApplyWings(float angle)
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
        for (var i = 0; i < Wings.Length; i++)
        {
            if (wingIndex[i] < 0)
                continue;

            var local = pose->AccessBoneLocalSpace(wingIndex[i]);
            var current = new Quaternion(local->Rotation.X, local->Rotation.Y, local->Rotation.Z, local->Rotation.W);
            var rotated = current * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, WingOpenSign * Wings[i].Side * angle);
            local->Rotation.X = rotated.X;
            local->Rotation.Y = rotated.Y;
            local->Rotation.Z = rotated.Z;
            local->Rotation.W = rotated.W;
        }

        // 書き換えた局所の回転から、子のボーン (翼の先) を含めてモデル空間を計算し直す
        pose->AccessSyncedPoseModelSpace();
    }

    private void IndexWings(hkaSkeleton* skeleton)
    {
        if (skeleton == indexedSkeleton)
            return;

        for (var i = 0; i < Wings.Length; i++)
            wingIndex[i] = -1;
        for (var bone = 0; bone < skeleton->Bones.Length; bone++)
        {
            var name = skeleton->Bones[bone].Name.String;
            for (var i = 0; i < Wings.Length; i++)
            {
                if (name == Wings[i].Name)
                    wingIndex[i] = bone;
            }
        }

        indexedSkeleton = skeleton;
        Plugin.Log.Debug($"翼のボーン: 左 {wingIndex[0]} 右 {wingIndex[1]}");
    }

    public void Dispose() => updateHook?.Dispose();
}
