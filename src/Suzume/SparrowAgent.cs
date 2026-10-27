using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Suzume.Core;

namespace Suzume;

/// <summary>このフレームで全部のスズメに共通する情報。</summary>
public readonly record struct FrameContext(
    float Dt,
    Vector3 LocalPlayerPosition,
    float LocalPlayerYaw,
    Vector3 CameraForward,
    bool LocalAggression,
    bool LocalJumping,
    bool LocalMounted);

/// <summary>
/// タビスズメ 1 羽ぶんの制御。行動の判断・地面・手まねきの検出などの状態を持ち、持ち主に付いていく。
/// 自分のスズメも他人のスズメも同じ仕組みで動かす。
/// </summary>
public sealed unsafe class SparrowAgent
{
    // 手まねきする (/beckon) で再生される ActionTimeline (emote/comeon)
    private const ushort BeckonTimelineId = 697;
    // GameObject.Height が取れないときの頭の高さ
    private const float FallbackHeadHeight = 1.7f;
    // スズメを驚かせる攻撃的なエモートの ActionTimeline:
    // 不満を表す /angry, 怒る /furious, 平手打ち /slap, 戦いに備える /battlestance,
    // 格闘訓練 /box, 地団駄を踏む /stomp, 投げる /throw
    private static readonly HashSet<ushort> AggressiveTimelineIds = [690, 639, 691, 664, 3752, 3755, 3778, 5809, 5814, 8179, 641];
    // 「スズメに向けて」とみなす範囲: ターゲットしているか、前方この距離・角度以内
    private const float ThreatDistance = 6f;
    private const float ThreatHalfAngleDeg = 40f;
    // 他人が騎乗してこれだけ地面から浮いていたら、飛んでいるとみなす
    private const float OwnerFlyingHeight = 1.5f;

    private readonly Configuration config;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly GroundProbe groundProbe;
    private readonly SparrowParams parameters = new();
    private readonly SparrowBrain brain;
    private readonly ChirpScheduler scheduler;
    private readonly PoseApplier applier = new();
    private readonly GroundTracker ground = new();
    private readonly RisingEdge beckon = new();
    private readonly RisingEdge aggression = new();
    private readonly HeadLocator headLocator = new();
    private Vector3? lastOwnerPosition;

    public SparrowAgent(Configuration config, ChirpPlayer? chirpPlayer, GroundProbe groundProbe, int seed)
    {
        this.config = config;
        this.chirpPlayer = chirpPlayer;
        this.groundProbe = groundProbe;
        var random = new SeededRandom(seed);
        brain = new SparrowBrain(parameters, random, groundProbe.GroundAt);
        scheduler = new ChirpScheduler(parameters, random);
    }

    public bool Active { get; private set; }
    public SparrowPose LastPose { get; private set; }
    public Vector3 LastTarget { get; private set; }

    public void ResetPosition() => brain.Invalidate();
    public void RequestDustBath() => brain.RequestDustBath();
    public void RequestPreen() => brain.RequestPreen();

    /// <summary>1 フレーム進め、見た目に書き込む。ボーンに入れるポーズを返す(止めたときは null)。</summary>
    public SparrowPose? Step(Companion* minion, Character* owner, bool isLocal, in FrameContext frame)
    {
        var dt = frame.Dt;
        var player = (GameObject*)owner;
        var ownerPosition = player->Position;
        var ownerVelocity = lastOwnerPosition is { } last && dt > 0f ? Vector3.Subtract(ownerPosition, last) / dt : Vector3.Zero;
        lastOwnerPosition = ownerPosition;

        // 自分は Condition から、他人はキャラクターの状態から、ジャンプと騎乗を読む
        var jumping = isLocal ? frame.LocalJumping : owner->IsJumping();
        var mounted = isLocal ? frame.LocalMounted : owner->IsMounted();

        // タビスズメの論理位置は宙に浮いているので、持ち主の足元から地上の目標地点を決める
        var feet = ownerPosition with { Y = ground.Update(ownerPosition.Y, jumping) };
        groundProbe.ReferenceY = feet.Y;

        // 他人が騎乗して空を飛んでいるときは、地面が分からないのでゲーム本来の動きに任せる
        if (!isLocal && mounted && groundProbe.GroundAt(ownerPosition) is { } under && ownerPosition.Y - under > OwnerFlyingHeight)
        {
            Suspend(minion);
            return null;
        }

        var target = FollowTarget.For(feet, player->Rotation);
        if (groundProbe.GroundAt(target) is { } targetGround)
            target.Y = targetGround;

        parameters.FlyDistance = config.FlyDistance;
        parameters.WanderRadiusMax = config.WanderDistance;
        parameters.ChirpFrequency = config.ChirpFrequency;

        var head = HeadPosition(player, ownerPosition);
        var beckoned = beckon.Update(PlaysAny(owner, BeckonTimelineId));
        // 持ち主の攻撃的なエモートと、あなたの攻撃的なエモート (自分のスズメなら同じもの) のどちらでも驚く
        var ownerAggression = aggression.Update(IsAggressive(owner));
        var threatened =
            (!isLocal && ownerAggression && Facing.IsInFront(ownerPosition, player->Rotation, LastPose.Position, ThreatDistance, ThreatHalfAngleDeg))
            || (frame.LocalAggression && (IsTargeted(minion)
                || Facing.IsInFront(frame.LocalPlayerPosition, frame.LocalPlayerYaw, LastPose.Position, ThreatDistance, ThreatHalfAngleDeg)));
        if (isLocal && threatened)
            Plugin.Log.Debug($"攻撃的なエモートを検出 (状態 {LastPose.State})");
        if (isLocal && beckoned)
            Plugin.Log.Debug($"手まねきを検出 (状態 {LastPose.State}) 頭={head:F2}");

        var pose = brain.Tick(new SparrowInput(
            dt, target, player->Rotation, ownerPosition, ownerVelocity, mounted, head, beckoned, jumping, threatened));
        if (isLocal && pose.State != LastPose.State)
            Plugin.Log.Debug($"状態 {LastPose.State} -> {pose.State} 位置={pose.Position:F2} 頭={head:F2}");

        applier.Apply(minion, pose);
        Active = true;
        LastPose = pose;
        LastTarget = target;

        // 鳴き声の大きさは、あなたからの距離で決める。左右と前後(後ろはこもる)は、あなたの位置でカメラの向きに耳を向けたときの聞こえ方
        var chirp = scheduler.Update(dt, pose.State, pose.Triggers);
        if (chirp is { } kind && config.SoundEnabled && chirpPlayer != null)
        {
            var placement = config.StereoSound
                ? ChirpStereo.Place(frame.LocalPlayerPosition, frame.CameraForward, pose.Position)
                : StereoPlacement.Centre;
            chirpPlayer.Play(kind, ChirpVolume.Compute(config.Volume, Vector3.Distance(frame.LocalPlayerPosition, pose.Position)), placement);
            LogPlacement(frame, pose.Position, placement);
        }

        return pose;
    }

    /// <summary>描画直前の書き込み(PoseApplier が Draw モードのときの前傾)。</summary>
    public void ApplyLate(Companion* minion)
    {
        if (Active)
            applier.ApplyLate(minion, LastPose);
    }

    /// <summary>上書きをやめてゲーム本来の見た目に戻し、次に動かすときは目標地点から始め直す。</summary>
    public void Suspend(Companion* minion)
    {
        if (Active && minion != null)
            applier.Release(minion);

        Active = false;
        brain.Invalidate();
        ground.Reset();
        beckon.Reset();
        aggression.Reset();
        lastOwnerPosition = null;
    }

    /// <summary>頭頂の位置。骨格から読み、読めなければ GameObject.Height で代用する。設定の補正を足す。</summary>
    private Vector3 HeadPosition(GameObject* player, Vector3 playerPosition)
    {
        var head = headLocator.Find(player)
            ?? playerPosition with { Y = playerPosition.Y + (player->Height is > 0.5f and < 4f ? player->Height : FallbackHeadHeight) };
        return head with { Y = head.Y + config.HeadOffset };
    }

    /// <summary>持ち主がそのアニメを再生中か(全身・上半身のどちらのスロットでも)。</summary>
    private static bool PlaysAny(Character* owner, ushort timelineId)
    {
        var sequencer = &owner->Timeline.TimelineSequencer;
        return sequencer->GetSlotTimeline(0) == timelineId || sequencer->GetSlotTimeline(1) == timelineId;
    }

    public static bool IsAggressive(Character* owner)
    {
        var sequencer = &owner->Timeline.TimelineSequencer;
        return AggressiveTimelineIds.Contains(sequencer->GetSlotTimeline(0))
            || AggressiveTimelineIds.Contains(sequencer->GetSlotTimeline(1));
    }

    /// <summary>左右の振り分けと、画面上でスズメがあなたのどちら側に映っているかを並べて記録する(左右が合っているかの確かめ)。</summary>
    private static void LogPlacement(FrameContext frame, Vector3 sparrow, StereoPlacement placement)
    {
        var onScreen = Plugin.GameGui.WorldToScreen(sparrow, out var sparrowScreen)
            & Plugin.GameGui.WorldToScreen(frame.LocalPlayerPosition, out var playerScreen);
        var side = !onScreen ? "画面外"
            : sparrowScreen.X > playerScreen.X ? $"右 {sparrowScreen.X - playerScreen.X:F0}px"
            : $"左 {playerScreen.X - sparrowScreen.X:F0}px";
        Plugin.Log.Debug($"鳴き声の定位: 音量 左 {placement.LeftGain:F2} 右 {placement.RightGain:F2} 遅れ 左 {placement.LeftDelaySamples} 右 {placement.RightDelaySamples} 後ろ {placement.Behind:F2} / 画面ではあなたの{side}");
    }

    private static bool IsTargeted(Companion* minion)
    {
        var target = Plugin.TargetManager.Target;
        return target != null && target.Address == (nint)minion;
    }
}
