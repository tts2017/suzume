using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Suzume.Core;

namespace Suzume;

/// <summary>毎フレーム SparrowBrain を進め、結果をミニオンの見た目と音に反映する。</summary>
public sealed unsafe class SparrowController : IDisposable
{
    // これだけ続けて例外が出たら自動で無効化する
    private const int MaxConsecutiveErrors = 10;
    // 手まねきする (/beckon) で再生される ActionTimeline (emote/comeon)
    private const ushort BeckonTimelineId = 697;
    // GameObject.Height が取れないときの頭の高さ
    private const float FallbackHeadHeight = 1.7f;

    private readonly Configuration config;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly SparrowParams parameters = new();
    private readonly SparrowBrain brain;
    private readonly ChirpScheduler scheduler;
    private readonly PoseApplier applier = new();
    private readonly GroundTracker ground = new();
    private readonly RisingEdge beckon = new();
    private readonly HeadLocator headLocator = new();
    // 調査用: 直前に見たプレイヤーのスロット 0〜2 のアニメ
    private readonly ushort[] lastPlayerTimelines = new ushort[3];
    private Vector3? lastPlayerPosition;
    private int consecutiveErrors;

    public SparrowController(Configuration config, ChirpPlayer? chirpPlayer)
    {
        this.config = config;
        this.chirpPlayer = chirpPlayer;
        var random = new SeededRandom(Environment.TickCount);
        brain = new SparrowBrain(parameters, random);
        scheduler = new ChirpScheduler(parameters, random);
    }

    public bool Active { get; private set; }
    public SparrowPose LastPose { get; private set; }
    public Vector3 LastTarget { get; private set; }

    public void Update(IFramework framework)
    {
        try
        {
            Step((float)framework.UpdateDelta.TotalSeconds);
            consecutiveErrors = 0;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "タビスズメの更新に失敗しました");
            ReleaseIfActive();
            if (++consecutiveErrors >= MaxConsecutiveErrors)
            {
                config.Enabled = false;
                config.Save();
                consecutiveErrors = 0;
                Plugin.ChatGui.PrintError("[Suzume] エラーが続いたため無効にしました。/xllog を確認してください。");
            }
        }
    }

    /// <summary>描画直前の書き込み(PoseApplier が Draw モードのときの前傾)。</summary>
    public void OnDraw()
    {
        if (!Active)
            return;

        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            applier.ApplyLate(minion, LastPose);
    }

    /// <summary>上書きをやめ、次に有効になったとき目標地点から始め直す。</summary>
    public void Suspend()
    {
        ReleaseIfActive();
        brain.Invalidate();
        ground.Reset();
        beckon.Reset();
        lastPlayerPosition = null;
    }

    private void Step(float dt)
    {
        Character* owner = null;
        var minion = config.Enabled && !IsSuspendedByGame() ? MinionTracker.FindSparrow(out owner) : null;
        if (minion == null || ((GameObject*)minion)->DrawObject == null)
        {
            Suspend();
            return;
        }

        var player = (GameObject*)owner;
        var playerPosition = player->Position;
        var playerVelocity = lastPlayerPosition is { } last && dt > 0f ? Vector3.Subtract(playerPosition, last) / dt : Vector3.Zero;
        lastPlayerPosition = playerPosition;

        // タビスズメの論理位置は宙に浮いているので、プレイヤーの足元から地上の目標地点を決める
        var jumping = Plugin.Condition[ConditionFlag.Jumping] || Plugin.Condition[ConditionFlag.Jumping61];
        var feet = playerPosition with { Y = ground.Update(playerPosition.Y, jumping) };
        var target = FollowTarget.For(feet, player->Rotation);

        parameters.FlyDistance = config.FlyDistance;
        parameters.ChirpFrequency = config.ChirpFrequency;

        var head = HeadPosition(player, playerPosition);
        LogPlayerTimelineChanges(owner);
        var beckoned = beckon.Update(IsBeckoning(owner));
        if (beckoned)
            Plugin.Log.Debug($"手まねきを検出 (状態 {LastPose.State}) Height={player->Height:F2} 頭={head:F2}");

        var pose = brain.Tick(new SparrowInput(
            dt, target, player->Rotation, playerPosition, playerVelocity, Plugin.Condition[ConditionFlag.Mounted],
            head, beckoned, jumping));
        if (pose.State != LastPose.State)
            Plugin.Log.Debug($"状態 {LastPose.State} -> {pose.State} 位置={pose.Position:F2} 頭={head:F2}");
        applier.Apply(minion, pose);
        Active = true;
        LastPose = pose;
        LastTarget = target;

        var chirp = scheduler.Update(dt, pose.State, pose.Triggers);
        if (chirp is { } kind && config.SoundEnabled && chirpPlayer != null)
            chirpPlayer.Play(kind, ChirpVolume.Compute(config.Volume, Vector3.Distance(playerPosition, pose.Position)));
    }

    /// <summary>頭頂の位置。骨格から読み、読めなければ GameObject.Height で代用する。設定の補正を足す。</summary>
    private Vector3 HeadPosition(GameObject* player, Vector3 playerPosition)
    {
        var head = headLocator.Find(player)
            ?? playerPosition with { Y = playerPosition.Y + (player->Height is > 0.5f and < 4f ? player->Height : FallbackHeadHeight) };
        return head with { Y = head.Y + config.HeadOffset };
    }

    /// <summary>プレイヤーが手まねきのエモートを再生中か(全身・上半身のどちらのスロットでも)。</summary>
    private static bool IsBeckoning(Character* owner)
    {
        var sequencer = &owner->Timeline.TimelineSequencer;
        return sequencer->GetSlotTimeline(0) == BeckonTimelineId
            || sequencer->GetSlotTimeline(1) == BeckonTimelineId;
    }

    /// <summary>調査用: プレイヤーのアニメが変わったらログに出す(手まねきの検出確認)。</summary>
    private void LogPlayerTimelineChanges(Character* owner)
    {
        var sequencer = &owner->Timeline.TimelineSequencer;
        for (var slot = 0u; slot < lastPlayerTimelines.Length; slot++)
        {
            var id = sequencer->GetSlotTimeline(slot);
            if (id == lastPlayerTimelines[slot])
                continue;

            lastPlayerTimelines[slot] = id;
            Plugin.Log.Debug($"プレイヤー slot{slot} = {id}");
        }
    }

    private void ReleaseIfActive()
    {
        if (!Active)
            return;

        Active = false;
        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            applier.Release(minion);
    }

    /// <summary>制御が動かないときの調査用。止めている条件を並べる。</summary>
    public string Diagnose()
    {
        var reasons = new List<string>();
        if (!config.Enabled)
            reasons.Add("設定で無効");
        if (Plugin.ClientState.IsGPosing)
            reasons.Add("GPose");
        foreach (var flag in SuspendFlags)
        {
            if (Plugin.Condition[flag])
                reasons.Add(flag.ToString());
        }

        return $"Active={Active} 停止理由=[{string.Join(", ", reasons)}] 連続エラー={consecutiveErrors}";
    }

    private static readonly ConditionFlag[] SuspendFlags =
    [
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.InFlight,
        ConditionFlag.Swimming,
        ConditionFlag.Diving,
    ];

    // 地面の高さが分からない(飛行・水中)ときと、演出中はゲーム本来の動きに任せる
    private static bool IsSuspendedByGame() =>
        Plugin.ClientState.IsGPosing
        || Plugin.Condition[ConditionFlag.BetweenAreas]
        || Plugin.Condition[ConditionFlag.BetweenAreas51]
        || Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        || Plugin.Condition[ConditionFlag.WatchingCutscene]
        || Plugin.Condition[ConditionFlag.WatchingCutscene78]
        || Plugin.Condition[ConditionFlag.InFlight]
        || Plugin.Condition[ConditionFlag.Swimming]
        || Plugin.Condition[ConditionFlag.Diving];

    public void Dispose() => ReleaseIfActive();
}
