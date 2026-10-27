using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Suzume.Core;

namespace Suzume;

/// <summary>
/// 周りのタビスズメを見つけ、1 羽ずつ SparrowAgent で動かす。自分のスズメに加え、設定によっては他人のスズメも。
/// </summary>
public sealed unsafe class SparrowController : IDisposable
{
    // これだけ続けて例外が出たら自動で無効化する
    private const int MaxConsecutiveErrors = 10;

    private readonly Configuration config;
    private readonly ChirpPlayer? chirpPlayer;
    private readonly BonePoser bonePoser;
    private readonly GroundProbe groundProbe = new();
    private readonly RisingEdge localAggression = new();
    // 持ち主の EntityId ごとのスズメ
    private readonly Dictionary<uint, SparrowAgent> agents = new();
    private int consecutiveErrors;
    // 調査用: 見つけた数が変わったらログに出す
    private (int All, int Own) lastFoundCount;

    public SparrowController(Configuration config, ChirpPlayer? chirpPlayer, BonePoser bonePoser)
    {
        this.config = config;
        this.chirpPlayer = chirpPlayer;
        this.bonePoser = bonePoser;
    }

    private SparrowAgent? OwnAgent =>
        Plugin.ObjectTable.LocalPlayer is { } local && agents.TryGetValue(local.EntityId, out var agent) ? agent : null;

    /// <summary>自分のスズメを動かしているか。</summary>
    public bool Active => OwnAgent?.Active == true;
    public SparrowPose LastPose => OwnAgent?.LastPose ?? default;
    public System.Numerics.Vector3 LastTarget => OwnAgent?.LastTarget ?? default;
    public int ActiveCount => agents.Values.Count(x => x.Active);

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
            Suspend();
            if (++consecutiveErrors >= MaxConsecutiveErrors)
            {
                config.Enabled = false;
                config.Save();
                consecutiveErrors = 0;
                Plugin.ChatGui.PrintError("[Suzume] エラーが続いたため無効にしました。/xllog を確認してください。");
            }
        }
    }

    /// <summary>描画直前の書き込み(PoseApplier が Draw モードのときの前傾)。自分のスズメだけ。</summary>
    public void OnDraw()
    {
        var minion = MinionTracker.FindSparrow(out _);
        if (minion != null)
            OwnAgent?.ApplyLate(minion);
    }

    /// <summary>自分のスズメをそばの地面に置き直す(地面に埋まったときなどの逃げ道)。</summary>
    public void ResetPosition() => OwnAgent?.ResetPosition();

    /// <summary>確認用: 自分のスズメに、次に地上で落ち着いたとき砂浴びを始めさせる。</summary>
    public void RequestDustBath() => OwnAgent?.RequestDustBath();

    /// <summary>確認用: 自分のスズメに、次に地上で落ち着いたとき毛づくろいを始めさせる。</summary>
    public void RequestPreen() => OwnAgent?.RequestPreen();

    /// <summary>すべてのスズメの上書きをやめる。</summary>
    public void Suspend()
    {
        foreach (var found in MinionTracker.FindAllSparrows())
        {
            if (agents.TryGetValue(found.OwnerId, out var agent))
                agent.Suspend((Companion*)found.Minion);
        }

        agents.Clear();
        bonePoser.Clear();
        localAggression.Reset();
    }

    private void Step(float dt)
    {
        var local = Plugin.ObjectTable.LocalPlayer;
        if (!config.Enabled || IsSuspendedByGame() || local == null)
        {
            Suspend();
            return;
        }

        var localCharacter = (Character*)local.Address;
        var localPlayer = (GameObject*)localCharacter;
        var localJumping = Plugin.Condition[ConditionFlag.Jumping] || Plugin.Condition[ConditionFlag.Jumping61];
        var localMounted = Plugin.Condition[ConditionFlag.Mounted];
        // 地面の当たり判定と見た目の差は、自分のキャラクターで測る
        groundProbe.ReferenceY = localPlayer->Position.Y;
        groundProbe.Calibrate(localPlayer->Position, grounded: !localJumping && !localMounted);

        var frame = new FrameContext(
            dt,
            localPlayer->Position,
            localPlayer->Rotation,
            localAggression.Update(SparrowAgent.IsAggressive(localCharacter)),
            localJumping,
            localMounted);

        var seen = new HashSet<uint>();
        var allFound = MinionTracker.FindAllSparrows();
        var count = (allFound.Count, allFound.Count(x => x.IsLocal));
        if (count != lastFoundCount)
        {
            lastFoundCount = count;
            Plugin.Log.Debug($"タビスズメ {count.Item1} 羽 (自分 {count.Item2} 羽): "
                + string.Join(", ", allFound.Select(x => $"[{x.ObjectIndex}] 持ち主 {x.OwnerId:X}{(x.IsLocal ? " (自分)" : "")} OwnerId={((GameObject*)x.Minion)->OwnerId:X}")));
        }

        foreach (var found in allFound)
        {
            var minion = (Companion*)found.Minion;
            seen.Add(found.OwnerId);
            if (!agents.TryGetValue(found.OwnerId, out var agent))
            {
                agent = new SparrowAgent(config, chirpPlayer, groundProbe, unchecked((int)found.OwnerId ^ Environment.TickCount));
                agents[found.OwnerId] = agent;
            }

            // 他人のスズメは設定で切り替える。自分が空を飛ぶ・水中にいるときは自分のスズメを止める
            var skip = found.IsLocal ? IsLocalAirborneOrInWater() : !config.IncludeOthers;
            var pose = skip ? null : agent.Step(minion, (Character*)found.Owner, found.IsLocal, frame);
            if (pose is { } p)
            {
                bonePoser.SetPose(found.ObjectIndex, found.Minion, p);
            }
            else
            {
                agent.Suspend(minion);
                bonePoser.Remove(found.ObjectIndex);
            }
        }

        // いなくなったスズメ (しまわれた・遠くへ行った) は忘れる。オブジェクトはもうないので書き戻しは要らない
        foreach (var gone in agents.Keys.Where(id => !seen.Contains(id)).ToList())
            agents.Remove(gone);
        bonePoser.RemoveMissing();
    }

    /// <summary>制御が動かないときの調査用。止めている条件を並べる。</summary>
    public string Diagnose()
    {
        var reasons = new List<string>();
        if (!config.Enabled)
            reasons.Add("設定で無効");
        if (Plugin.ClientState.IsGPosing)
            reasons.Add("GPose");
        foreach (var flag in SuspendFlags.Concat(LocalAirFlags))
        {
            if (Plugin.Condition[flag])
                reasons.Add(flag.ToString());
        }

        return $"Active={Active} 動かしている数={ActiveCount} 停止理由=[{string.Join(", ", reasons)}] 連続エラー={consecutiveErrors}";
    }

    // 演出中・移動中は、全部のスズメをゲーム本来の動きに任せる
    private static readonly ConditionFlag[] SuspendFlags =
    [
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.OccupiedInCutSceneEvent,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
    ];

    // 自分が飛行・水中にいるときは地面が分からないので、自分のスズメだけゲーム本来の動きに任せる
    private static readonly ConditionFlag[] LocalAirFlags =
    [
        ConditionFlag.InFlight,
        ConditionFlag.Swimming,
        ConditionFlag.Diving,
    ];

    private static bool IsSuspendedByGame() =>
        Plugin.ClientState.IsGPosing || SuspendFlags.Any(flag => Plugin.Condition[flag]);

    private static bool IsLocalAirborneOrInWater() => LocalAirFlags.Any(flag => Plugin.Condition[flag]);

    public void Dispose() => Suspend();
}
