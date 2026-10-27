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
    // その場で羽ばたく姿勢 (normal/idle → cbnm_id0)。砂浴びのばたつきに使う
    private static readonly ushort FlutterTimelineId = 3;
    // DrawOffset はモデルの向きに合わせて回る
    private static readonly bool DrawOffsetIsLocal = true;
    private static readonly PitchMode PitchWrite = PitchMode.Update;
    // 正の pitch で頭が下がる
    private static readonly float PitchSign = 1f;
    // ゲームが平常時に入れている DrawOffset
    private static readonly Vector3 BaseDrawOffset = Vector3.Zero;

    // 最後に再生させたアニメ。0 は未再生
    private ushort lastTimelineId;

    // 羽ばたきアニメ (3) は体を宙に持ち上げるので、胴のボーンの高さを止まり姿勢と比べて差だけ下げる
    private const long StableIdleMilliseconds = 500;
    private readonly BoneLocator body = new("n_hara");
    private float? perchedBodyHeight;
    private long idleAnimSince = -1;
    private long lastLiftLog;

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
        offset.Y -= BodyLift(go, pose);
        go->SetDrawOffset(offset.X, offset.Y, offset.Z);

        var timelineId = pose.Anim switch
        {
            SparrowAnim.Fly => FlyTimelineId,
            SparrowAnim.Flutter => FlutterTimelineId,
            _ => IdleTimelineId,
        };
        var timeline = &((Character*)minion)->Timeline;
        timeline->BaseOverride = timelineId;
        // AI を止めているとゲームがベースアニメを選び直さないので、切り替わったときに 1 回だけ直接再生する
        if (lastTimelineId != timelineId)
        {
            timeline->TimelineSequencer.PlayTimeline(timelineId, null);
            lastTimelineId = timelineId;
        }

        if (PitchWrite == PitchMode.Update)
            WritePitch(go, pose);
    }

    /// <summary>
    /// 止まり姿勢のときより胴が高く持ち上がっている量。飛行中は高さを行動側で決めているので 0。
    /// 止まり姿勢が 0.5 秒続いたときの胴の高さを基準にする(アニメの切り替えの混ざりを避ける)。
    /// </summary>
    private float BodyLift(GameObject* go, in SparrowPose pose)
    {
        var height = body.HeightAboveOrigin(go);
        if (height is not { } current)
            return 0f;

        var now = Environment.TickCount64;
        if (pose.Anim == SparrowAnim.Idle)
        {
            if (idleAnimSince < 0)
                idleAnimSince = now;
            if (now - idleAnimSince >= StableIdleMilliseconds)
                perchedBodyHeight = current;
        }
        else
        {
            idleAnimSince = -1;
        }

        if (pose.Anim == SparrowAnim.Fly || perchedBodyHeight is not { } perched)
            return 0f;

        var lift = MathF.Max(0f, current - perched);
        if (pose.Anim == SparrowAnim.Flutter && lift > 0.01f && now - lastLiftLog > 1000)
        {
            lastLiftLog = now;
            Plugin.Log.Debug($"羽ばたきで胴が {lift:F2} 持ち上がっている分を下げます (止まり {perched:F2} / 今 {current:F2})");
        }

        return lift;
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
        var go = (GameObject*)minion;
        go->SetDrawOffset(BaseDrawOffset.X, BaseDrawOffset.Y, BaseDrawOffset.Z);
        ((Character*)minion)->Timeline.BaseOverride = 0;
        lastTimelineId = 0;
        ClearPitch(go);
    }

    /// <summary>前傾は書き込んだまま残るので、向きだけの回転に戻す。</summary>
    public static void ClearPitch(GameObject* go)
    {
        var draw = go->DrawObject;
        if (draw != null)
            draw->Object.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, go->Rotation);
    }

    private static void WritePitch(GameObject* go, in SparrowPose pose)
    {
        var draw = go->DrawObject;
        if (draw != null)
            draw->Object.Rotation = Quaternion.CreateFromYawPitchRoll(pose.Yaw, PitchSign * pose.Pitch, pose.Roll);
    }
}
