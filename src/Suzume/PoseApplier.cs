using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Suzume.Core;

namespace Suzume;

public enum PitchMode
{
    /// <summary>Framework.Update で書き込む</summary>
    Update,
    /// <summary>描画直前 (UiBuilder.Draw) で書き込む</summary>
    Draw,
    /// <summary>前傾を使わない</summary>
    Off,
}

/// <summary>
/// SparrowPose をミニオンの見た目に書き込む。
/// 各定数の値は実機スパイクの結果 (docs/superpowers/plans/2026-10-27-spike-results.md) で決めた。
/// </summary>
public sealed unsafe class PoseApplier
{
    // idle_sp/normal_idle_sp_1。タビスズメが羽を畳んで止まる姿勢 (cbnm_idle_sp1)
    private static readonly ushort IdleTimelineId = 212;
    // 羽ばたいて飛ぶ姿勢
    private static readonly ushort FlyTimelineId = 22;
    // DrawOffset はモデルの向きに合わせて回る
    private static readonly bool DrawOffsetIsLocal = true;
    private static readonly PitchMode PitchWrite = PitchMode.Update;
    // 正の pitch で頭が下がる
    private static readonly float PitchSign = 1f;
    // ゲームが平常時に入れている DrawOffset
    private static readonly Vector3 BaseDrawOffset = Vector3.Zero;

    public void Apply(Companion* minion, in SparrowPose pose)
    {
        var go = (GameObject*)minion;

        // 論理位置を見た目の真上に寄せ、AI がそこに着いたことにして勝手に動かない(向きも変えない)ようにする。
        // 高さはゲームの値(宙に浮いている)のまま残し、差分だけ DrawOffset で下げる
        go->SetPosition(pose.Position.X, go->Position.Y, pose.Position.Z);
        minion->MoveDestination = go->Position;
        go->SetRotation(pose.Yaw);

        var offset = Vector3.Subtract(pose.Position, go->Position);
        if (DrawOffsetIsLocal)
            offset = Vector3.Transform(offset, Quaternion.CreateFromAxisAngle(Vector3.UnitY, -pose.Yaw));
        offset += BaseDrawOffset;
        go->SetDrawOffset(offset.X, offset.Y, offset.Z);

        ((Character*)minion)->Timeline.BaseOverride =
            pose.Anim == SparrowAnim.Fly ? FlyTimelineId : IdleTimelineId;

        if (PitchWrite == PitchMode.Update)
            WritePitch(go, pose);
    }

    /// <summary>描画直前に呼ぶ。PitchWrite が Draw のときだけ前傾を書き込む。</summary>
    public void ApplyLate(Companion* minion, in SparrowPose pose)
    {
        if (PitchWrite == PitchMode.Draw)
            WritePitch((GameObject*)minion, pose);
    }

    /// <summary>ゲーム本来の見た目に戻す。論理位置は AI がそのまま引き継ぐ。</summary>
    public void Release(Companion* minion)
    {
        ((GameObject*)minion)->SetDrawOffset(BaseDrawOffset.X, BaseDrawOffset.Y, BaseDrawOffset.Z);
        ((Character*)minion)->Timeline.BaseOverride = 0;
    }

    private static void WritePitch(GameObject* go, in SparrowPose pose)
    {
        var draw = go->DrawObject;
        if (draw != null)
            draw->Object.Rotation = Quaternion.CreateFromYawPitchRoll(pose.Yaw, PitchSign * pose.Pitch, 0f);
    }
}
