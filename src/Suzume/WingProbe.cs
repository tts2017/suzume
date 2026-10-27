using System.Numerics;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

namespace Suzume;

/// <summary>
/// スパイク: ボーンをどのタイミングで書き換えれば描画に残るかを確かめる。
/// /suzume wing &lt;update|draw|anim|finish|off&gt; で、翼 (j_ude_a_l/r) を 1 秒に 3 回 ±60° ばたつかせる。
/// </summary>
public sealed unsafe class WingProbe : IDisposable
{
    private static readonly string[] WingBones = ["j_ude_a_l", "j_ude_a_r"];

    private readonly Hook<Skeleton.Delegates.UpdateAnimations>? updateHook;
    private readonly Hook<Skeleton.Delegates.FinishAnimation>? finishHook;
    private readonly Dictionary<string, int> boneIndex = new();
    private hkaSkeleton* indexedSkeleton;
    private float time;

    public string Mode { get; set; } = "off";

    public WingProbe()
    {
        var update = (nint)Skeleton.Addresses.UpdateAnimations.Value;
        if (update != 0)
        {
            updateHook = Plugin.Interop.HookFromAddress<Skeleton.Delegates.UpdateAnimations>(update, UpdateAnimationsDetour);
            updateHook.Enable();
        }

        var finish = (nint)Skeleton.Addresses.FinishAnimation.Value;
        if (finish != 0)
        {
            finishHook = Plugin.Interop.HookFromAddress<Skeleton.Delegates.FinishAnimation>(finish, FinishAnimationDetour);
            finishHook.Enable();
        }

        Plugin.Log.Debug($"WingProbe: UpdateAnimations={update:X} FinishAnimation={finish:X}");
    }

    public void OnUpdate(float dt)
    {
        time += dt;
        if (Mode == "update")
            Apply(null);
    }

    public void OnDraw()
    {
        if (Mode == "draw")
            Apply(null);
    }

    private void UpdateAnimationsDetour(Skeleton* first, float deltaTime)
    {
        updateHook!.Original(first, deltaTime);
        if (Mode == "anim")
            Apply(null);
    }

    private void FinishAnimationDetour(Skeleton* skeleton, float deltaTime)
    {
        finishHook!.Original(skeleton, deltaTime);
        if (Mode == "finish")
            Apply(skeleton);
    }

    /// <summary>onlyFor を指定したときは、その骨格がタビスズメのものだけ書き換える。</summary>
    private void Apply(Skeleton* onlyFor)
    {
        try
        {
            var minion = MinionTracker.FindSparrow(out _);
            if (minion == null)
                return;

            var draw = ((GameObject*)minion)->DrawObject;
            if (draw == null || draw->Object.GetObjectType() != FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType.CharacterBase)
                return;

            var skeleton = ((CharacterBase*)draw)->Skeleton;
            if (skeleton == null || skeleton->PartialSkeletonCount == 0 || (onlyFor != null && onlyFor != skeleton))
                return;

            var pose = skeleton->PartialSkeletons[0].GetHavokPose(0);
            if (pose == null || pose->Skeleton == null)
                return;

            IndexBones(pose->Skeleton);
            var angle = MathF.PI / 3f * MathF.Sin(time * MathF.Tau * 3f);
            var sign = 1f;
            foreach (var name in WingBones)
            {
                if (boneIndex.TryGetValue(name, out var index))
                {
                    var local = pose->AccessBoneLocalSpace(index);
                    var q = new Quaternion(local->Rotation.X, local->Rotation.Y, local->Rotation.Z, local->Rotation.W);
                    var rotated = q * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, sign * angle);
                    local->Rotation.X = rotated.X;
                    local->Rotation.Y = rotated.Y;
                    local->Rotation.Z = rotated.Z;
                    local->Rotation.W = rotated.W;
                }

                sign = -sign;
            }

            pose->AccessSyncedPoseModelSpace();
        }
        catch (Exception ex)
        {
            Mode = "off";
            Plugin.Log.Error(ex, "WingProbe の書き込みに失敗しました");
        }
    }

    private void IndexBones(hkaSkeleton* skeleton)
    {
        if (skeleton == indexedSkeleton)
            return;

        boneIndex.Clear();
        for (var i = 0; i < skeleton->Bones.Length; i++)
        {
            var name = skeleton->Bones[i].Name.String;
            if (name != null && WingBones.Contains(name))
                boneIndex[name] = i;
        }

        indexedSkeleton = skeleton;
        Plugin.Log.Debug($"WingProbe: 翼のボーン {string.Join(", ", boneIndex.Select(x => $"{x.Key}={x.Value}"))}");
    }

    public void Dispose()
    {
        updateHook?.Dispose();
        finishHook?.Dispose();
    }
}
