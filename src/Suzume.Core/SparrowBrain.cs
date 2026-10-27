using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 実際のスズメ(地上は両足ホップ、遠くへは波状飛行)を真似る状態機械。
/// 目標地点 (Target) を、ホップや飛行で追いかける「見た目の位置」を計算する。
/// プレイヤーが止まっていれば周りを渡り歩き、手招きされたら頭に乗る。
/// </summary>
public sealed class SparrowBrain
{
    // ロード明けなどの巨大なフレーム時間で瞬間移動しないように抑える
    private const float MaxDt = 0.1f;
    private const float Epsilon = 1e-3f;

    private readonly SparrowParams p;
    private readonly IRandom rng;
    private readonly Func<Vector3, float?>? groundAt;

    private bool initialized;
    private Vector3 position;
    private float yaw;
    private float pitch;
    private float roll;
    private float wingSpread;
    private float wingFlap;

    // 向きを変える途中 (ワープせず、なめらかに・大きければ跳ねながら回る)
    private bool turning;
    private float turnFrom;
    private float turnDelta;
    private float turnTime;
    private float turnDuration;
    private float turnHopHeight;
    private float turnLift;

    // 見た目に出す前傾・傾き・翼の開き (急な切り替えをなめらかにしたもの)
    private float shownPitch;
    private float shownRoll;
    private float shownSpread;
    private float wingSide;
    private float shownWingSide;

    // 首と尾 (ボーン): 首は目標へなめらかに、つつく上下動はあとから上乗せする
    private float neckYawTarget;
    private float neckPitchTarget;
    private float neckNibble;
    private float shownNeckYaw;
    private float shownNeckPitch;
    private float shownNeckRoll;
    private float headTilt;
    private float headTiltHold;
    private float tailLift;
    private float tailFlickAmplitude;
    private float tailFlickDuration;
    private float tailFlickTime = float.MaxValue;

    // 毛づくろい
    private float preenSide;
    private bool preenWing;
    private float preenSwitchAt;
    // 見た目だけ沈める深さ(砂浴びの腹ばい)。position そのものは地面のまま
    private float crouch;
    private SparrowState state;
    private SparrowAnim anim;
    private SparrowTrigger triggers;
    private float stateTime;
    private float stateDuration;

    // Idle
    private float scanCountdown;
    private bool settling;

    // Hop / Alarm
    private int hopsLeft;
    private Hop? hop;
    private float hopTime;
    private float hopGapLeft;
    private Vector3 fleeDirection;

    // DustBath
    private bool dustBathArmed;
    private bool dustBathRequested;
    private bool preenRequested;
    private float dustBathCountdown;
    private bool dustFluttering;
    private float dustPhaseLeft;
    private bool shaking;
    private float shakeTime;
    private float dustBaseYaw;

    // Forage / Preen
    private int pecks;

    // TakeOff / Fly / Land
    private bool flapping;
    private float flightPhaseLeft;
    private float groundY;
    private float altitude;
    private float landStartAltitude;
    private float landStartSpeed;

    private float alarmCooldown;

    // 動き回る・頭に乗る。goal はこの Tick で向かう地点
    private Vector3 goal;
    private float playerStillTime;
    private bool hasWanderPoint;
    private Vector3 wanderPoint;
    private float wanderCountdown;
    private bool perchIntent;
    // 攻撃的なエモートで逃げた先。様子をうかがう間はここを目標にする
    private bool scared;
    private Vector3 scarePoint;
    private float waryCountdown;
    // 様子見が終わって戻る途中。離れていればホップではなく飛んで戻る
    private bool returning;
    private Vector3 landStartPosition;

    /// <param name="groundAt">その場所の地面の高さ。分からなければ null を返す。省略時は目標地点の高さで代用する</param>
    public SparrowBrain(SparrowParams parameters, IRandom random, Func<Vector3, float?>? groundAt = null)
    {
        p = parameters;
        rng = random;
        this.groundAt = groundAt;
    }

    /// <summary>次の Tick で論理位置へ置き直す(ミニオンの再召喚・一時停止明け)。</summary>
    public void Invalidate() => initialized = false;

    /// <summary>次に地上で落ち着いたとき、すぐに砂浴びを始める(確認用)。</summary>
    public void RequestDustBath() => dustBathRequested = true;

    /// <summary>次に地上で落ち着いたとき、すぐに毛づくろいを始める(確認用)。</summary>
    public void RequestPreen() => preenRequested = true;

    public SparrowPose Tick(in SparrowInput input)
    {
        triggers = SparrowTrigger.None;
        var dt = Math.Clamp(input.Dt, 0f, MaxDt);

        if (!initialized || Vector3.Distance(position, input.Target) > p.ResetDistance)
        {
            Reset(input);
            return CurrentPose();
        }

        stateTime += dt;
        alarmCooldown = MathF.Max(0f, alarmCooldown - dt);

        UpdateWander(input, dt);
        UpdateDustBathTimer(dt);
        UpdateWary(dt);
        goal = Goal(input);
        if (input.Threatened)
            OnThreatened(input);
        else if (input.PerchRequested)
            OnBeckoned(input);

        switch (state)
        {
            case SparrowState.Idle:
                TickIdle(input, dt);
                break;
            case SparrowState.Hop:
            case SparrowState.Alarm:
                TickHop(input, dt);
                break;
            case SparrowState.Forage:
                TickForage(input);
                break;
            case SparrowState.Preen:
                TickPreen(input);
                break;
            case SparrowState.TakeOff:
                TickTakeOff(input, dt);
                break;
            case SparrowState.Fly:
                TickFly(input, dt);
                break;
            case SparrowState.Land:
                TickLand(input, dt);
                break;
            case SparrowState.Perched:
                TickPerched(input, dt);
                break;
            case SparrowState.DustBath:
                TickDustBath(input, dt);
                break;
        }

        SnapToGround();
        AdvanceTurn(dt);
        yaw = Angles.Wrap(yaw);
        UpdateGestures(dt);
        SmoothPresentation(dt);
        return CurrentPose();
    }

    // ---- 首・尾のしぐさ ----

    /// <summary>首をかしげる時間・尾の跳ね上げを進め、毛づくろい以外では首を正面に戻す。</summary>
    private void UpdateGestures(float dt)
    {
        if (state != SparrowState.Preen)
        {
            neckYawTarget = 0f;
            neckPitchTarget = 0f;
            // つつく上下動は、急に止めずに静かに収める
            neckNibble = Approach(neckNibble, 0f, dt, p.NeckSmoothing * 0.5f);
        }

        headTiltHold -= dt;
        if (headTiltHold <= 0f || state is SparrowState.TakeOff or SparrowState.Fly or SparrowState.Land or SparrowState.Preen)
            headTilt = 0f;

        tailFlickTime += dt;
        tailLift = tailFlickTime < tailFlickDuration
            ? tailFlickAmplitude * MathF.Sin(MathF.PI * tailFlickTime / tailFlickDuration)
            : 0f;
    }

    private void FlickTail(float degrees, float duration)
    {
        tailFlickAmplitude = Angles.DegToRad(degrees);
        tailFlickDuration = duration;
        tailFlickTime = 0f;
    }

    // ---- なめらかな向き変え ----

    /// <summary>
    /// newYaw へ向きを変え始める。duration を省くと角度で決める: 大きければその場で小さく跳ねながら、小さければ素早く回る。
    /// どの場合も、回る速さは TurnMaxRateDeg を超えない。
    /// </summary>
    private void BeginTurn(float newYaw, float? duration = null, bool allowHop = true)
    {
        var from = turning ? turnFrom + turnDelta : yaw;
        var delta = Angles.Wrap(newYaw - from);
        if (MathF.Abs(delta) < Epsilon)
            return;

        var big = MathF.Abs(delta) >= Angles.DegToRad(p.TurnHopThresholdDeg);
        turning = true;
        turnFrom = yaw;
        turnDelta = Angles.Wrap(newYaw - yaw);
        turnTime = 0f;
        var minimum = MathF.Abs(turnDelta) / Angles.DegToRad(p.TurnMaxRateDeg);
        turnDuration = MathF.Max(duration ?? (big ? p.TurnHopDuration : p.TurnQuickDuration), minimum);
        // 跳ねるのは、その場で向きを変えるときだけ(ホップや飛行と重ねない)
        turnHopHeight = allowHop && duration == null && big ? p.TurnHopHeight : 0f;
    }

    private void CancelTurn()
    {
        turning = false;
        turnLift = 0f;
    }

    private void AdvanceTurn(float dt)
    {
        if (!turning)
            return;

        turnTime += dt;
        var u = MathF.Min(1f, turnTime / turnDuration);
        yaw = turnFrom + turnDelta * (u * u * (3f - 2f * u));
        turnLift = turnHopHeight * 4f * u * (1f - u);
        if (u >= 1f)
            CancelTurn();
    }

    /// <summary>前傾・傾き・翼の開き・首の急な切り替えを、短い時間でなめらかにつなぐ。</summary>
    private void SmoothPresentation(float dt)
    {
        shownPitch = Approach(shownPitch, pitch, dt, p.PitchSmoothing);
        shownRoll = Approach(shownRoll, roll, dt, p.RollSmoothing);
        shownSpread = Approach(shownSpread, wingSpread, dt, p.WingSpreadSmoothing);
        // 翼を開いている側は、閉じ切るまで覚えておく(閉じる途中で反対の翼が動かないように)
        if (wingSpread > 0f)
            shownWingSide = wingSide;
        else if (shownSpread == 0f)
            shownWingSide = 0f;

        var maxNeckStep = Angles.DegToRad(p.NeckMaxRateDeg) * dt;
        shownNeckYaw = ApproachLimited(shownNeckYaw, neckYawTarget, dt, p.NeckSmoothing, maxNeckStep);
        shownNeckPitch = ApproachLimited(shownNeckPitch, neckPitchTarget, dt, p.NeckSmoothing, maxNeckStep);
        shownNeckRoll = ApproachLimited(shownNeckRoll, headTilt, dt, p.NeckSmoothing, maxNeckStep);
    }

    private static float ApproachLimited(float shown, float target, float dt, float smoothing, float maxStep)
    {
        var next = Approach(shown, target, dt, smoothing);
        return shown + Math.Clamp(next - shown, -maxStep, maxStep);
    }

    private static float Approach(float shown, float target, float dt, float smoothing)
    {
        var next = shown + (target - shown) * MathF.Min(1f, dt / MathF.Max(smoothing, Epsilon));
        // 0 に戻すときは、十分近づいたらぴったり 0 にする
        return target == 0f && MathF.Abs(next) < 0.005f ? 0f : next;
    }

    /// <summary>翼のばたつきは、開き具合に合わせて出し入れする(開いていない翼は振らない)。</summary>
    private float ShownFlap() =>
        wingSpread > 0f ? wingFlap * MathF.Min(1f, shownSpread / wingSpread)
        : p.DustWingSpreadDeg > 0f ? wingFlap * MathF.Min(1f, shownSpread / Angles.DegToRad(p.DustWingSpreadDeg))
        : 0f;

    /// <summary>地上で止まっている間は、足元の地面の高さに合わせる(段差などでずれを残さない)。</summary>
    private void SnapToGround()
    {
        var grounded = state is SparrowState.Idle or SparrowState.Forage or SparrowState.Preen or SparrowState.DustBath
            || (state is SparrowState.Hop or SparrowState.Alarm && hop == null);
        if (grounded && groundAt?.Invoke(position) is { } ground)
            position.Y = ground;
    }

    private SparrowPose CurrentPose() =>
        new(position + new Vector3(0f, turnLift - crouch, 0f), yaw, shownPitch, anim, state, triggers, shownRoll, shownSpread, ShownFlap(),
            shownWingSide, shownNeckYaw, shownNeckPitch + neckNibble, shownNeckRoll, tailLift);

    private void Reset(in SparrowInput input)
    {
        initialized = true;
        position = input.Target;
        yaw = Angles.Wrap(input.TargetYaw);
        CancelTurn();
        shownPitch = 0f;
        shownRoll = 0f;
        shownSpread = 0f;
        shownWingSide = 0f;
        shownNeckYaw = 0f;
        shownNeckPitch = 0f;
        shownNeckRoll = 0f;
        headTilt = 0f;
        tailFlickTime = float.MaxValue;
        hop = null;
        alarmCooldown = 0f;
        perchIntent = false;
        scared = false;
        returning = false;
        hasWanderPoint = false;
        playerStillTime = 0f;
        goal = input.Target;
        scanCountdown = rng.Range(p.ScanIntervalMin, p.ScanIntervalMax);
        EnterIdle();
    }

    private void SetState(SparrowState next)
    {
        state = next;
        stateTime = 0f;
        roll = 0f;
        crouch = 0f;
        wingSpread = 0f;
        wingFlap = 0f;
        wingSide = 0f;
    }

    // ---- 動き回る・頭に乗る ----

    private Vector3 Goal(in SparrowInput input) =>
        scared ? scarePoint
        : perchIntent ? input.HeadPosition
        : hasWanderPoint ? wanderPoint
        : input.Target;

    /// <summary>地点の高さを、その場所の地面の高さにする。分からなければ渡された高さのまま。</summary>
    private Vector3 OnGround(Vector3 point) =>
        groundAt?.Invoke(point) is { } y ? point with { Y = y } : point;

    /// <summary>攻撃的なエモートをされた: 警戒の声を上げ、プレイヤーと反対へ飛んで逃げる(頭の上からでも)。</summary>
    private void OnThreatened(in SparrowInput input)
    {
        var away = Angles.Horizontal(position - input.PlayerPosition);
        // 頭の上などで真上にいるときは、プレイヤーの向きの反対へ逃げる
        var direction = away.Length() > 0.3f
            ? Vector3.Normalize(away)
            : -new Vector3(MathF.Sin(input.TargetYaw), 0f, MathF.Cos(input.TargetYaw));

        scared = true;
        perchIntent = false;
        var flee = input.PlayerPosition + direction * (Angles.HorizontalDistance(position, input.PlayerPosition)
            + rng.Range(p.ScareFleeDistanceMin, p.ScareFleeDistanceMax));
        scarePoint = OnGround(flee with { Y = input.Target.Y });
        waryCountdown = rng.Range(p.WaryDurationMin, p.WaryDurationMax);
        alarmCooldown = p.AlarmCooldown;
        triggers |= SparrowTrigger.Alarm;
        goal = Goal(input);

        // 飛んでいる途中なら行き先が変わるだけ
        if (state is not (SparrowState.TakeOff or SparrowState.Fly or SparrowState.Land))
            EnterTakeOff(input);
    }

    /// <summary>逃げた先に降りてから、しばらく様子をうかがったら元の暮らしに戻る。</summary>
    private void UpdateWary(float dt)
    {
        if (!scared || state is SparrowState.TakeOff or SparrowState.Fly or SparrowState.Land)
            return;

        waryCountdown -= dt;
        if (waryCountdown <= 0f)
        {
            scared = false;
            returning = true;
        }
    }

    /// <summary>プレイヤーがしばらく止まっていたら、周りの地点をときどき選び直して渡り歩く。</summary>
    private void UpdateWander(in SparrowInput input, float dt)
    {
        var still = Angles.Horizontal(input.PlayerVelocity).Length() < p.StillSpeed;
        playerStillTime = still ? playerStillTime + dt : 0f;
        if (playerStillTime < p.WanderDelay)
        {
            hasWanderPoint = false;
            return;
        }

        wanderCountdown -= dt;
        if (hasWanderPoint && wanderCountdown > 0f)
            return;

        var angle = rng.Range(0f, MathF.Tau);
        var radius = rng.Range(p.WanderRadiusMin, p.WanderRadiusMax);
        wanderPoint = OnGround(new Vector3(
            input.PlayerPosition.X + MathF.Sin(angle) * radius,
            input.Target.Y,
            input.PlayerPosition.Z + MathF.Cos(angle) * radius));
        wanderCountdown = rng.Range(p.WanderIntervalMin, p.WanderIntervalMax);
        hasWanderPoint = true;
    }

    /// <summary>手招き: 頭の上にいれば下り、そうでなければ頭へ向かう。</summary>
    private void OnBeckoned(in SparrowInput input)
    {
        if (state == SparrowState.Perched)
        {
            perchIntent = false;
            goal = Goal(input);
            EnterTakeOff(input);
            return;
        }

        if (perchIntent)
            return;

        perchIntent = true;
        goal = Goal(input);
        // 飛んでいる途中なら行き先が頭に変わるだけ
        if (state is SparrowState.Idle or SparrowState.Hop or SparrowState.Forage or SparrowState.Preen
            or SparrowState.Alarm or SparrowState.DustBath)
            EnterTakeOff(input);
    }

    private void EnterPerched()
    {
        SetState(SparrowState.Perched);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        position = goal;
    }

    private void TickPerched(in SparrowInput input, float dt)
    {
        // 走る・跳ぶ・騎乗したら飛び立つ。歩いている間は頭の上で揺られる
        var running = Angles.Horizontal(input.PlayerVelocity).Length() > p.LandPlayerSpeed;
        if (running || input.PlayerJumping || input.PlayerMounted)
        {
            perchIntent = false;
            goal = Goal(input);
            EnterTakeOff(input);
            return;
        }

        position = goal;
        Scan(dt);
    }

    // ---- 割り込み ----

    /// <summary>地上にいる間の割り込み(警戒・飛び立ち)。遷移したら true。</summary>
    private bool TryInterrupt(in SparrowInput input)
    {
        if (ShouldAlarm(input))
        {
            EnterAlarm(input);
            return true;
        }

        if (ShouldFly(input))
        {
            EnterTakeOff(input);
            return true;
        }

        return false;
    }

    private bool ShouldFly(in SparrowInput input)
    {
        var gap = Angles.HorizontalDistance(position, goal);
        var fast = input.PlayerMounted || Angles.Horizontal(input.PlayerVelocity).Length() > p.SprintSpeed;
        return gap > p.FlyDistance
            || ((fast || returning) && gap > p.FastFlyDistance)
            || MathF.Abs(goal.Y - position.Y) > p.FlyHeightTrigger;
    }

    /// <summary>プレイヤーが速く、こちらへ向かって近づいてきたら警戒する。</summary>
    private bool ShouldAlarm(in SparrowInput input)
    {
        if (alarmCooldown > 0f)
            return false;

        var toBird = Angles.Horizontal(position - input.PlayerPosition);
        var velocity = Angles.Horizontal(input.PlayerVelocity);
        var distance = toBird.Length();
        var speed = velocity.Length();
        if (distance > p.AlarmDistance || distance < Epsilon || speed < p.AlarmPlayerSpeed)
            return false;

        return Vector3.Dot(velocity / speed, toBird / distance) > 0.7f;
    }

    private bool NeedsToMove(in SparrowInput input) =>
        Angles.HorizontalDistance(position, goal) > p.ArriveDistance
        || MathF.Abs(goal.Y - position.Y) > p.StepHeight;

    // ---- Idle ----

    private void EnterIdle(bool afterLanding = false)
    {
        SetState(SparrowState.Idle);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        settling = afterLanding;
        stateDuration = rng.Range(p.IdleMin, p.IdleMax);
        if (stateDuration >= p.LongIdleThreshold)
            triggers |= SparrowTrigger.LongIdleStart;
    }

    private void TickIdle(in SparrowInput input, float dt)
    {
        if (TryInterrupt(input))
            return;

        // 着地直後は尾を上げるように少し前傾する(v2 で尾のボーン操作に置き換える)
        pitch = settling && stateTime < p.LandSettleDuration
            ? Angles.DegToRad(p.LandPitchDeg) * MathF.Sin(MathF.PI * stateTime / p.LandSettleDuration)
            : 0f;

        Scan(dt);

        if (stateTime < stateDuration)
            return;

        // 砂浴びの時間が来ていたら、渡り歩きの途中でもその場で始める
        if (dustBathRequested || (dustBathArmed && dustBathCountdown <= 0f))
            EnterDustBath();
        else if (preenRequested)
            EnterPreen();
        else if (NeedsToMove(input))
            EnterHop(input);
        else if (rng.Chance(p.ForageChance))
            EnterForage();
        else if (rng.Chance(p.PreenChance))
            EnterPreen();
        else
            EnterIdle();
    }

    private void Scan(float dt)
    {
        scanCountdown -= dt;
        if (scanCountdown > 0f)
            return;

        // キョロキョロ: 素早く向きを変える。大きく振り向くときはその場で小さく跳ねる
        var sign = rng.Chance(0.5f) ? 1f : -1f;
        // 頭の上では跳ねない
        BeginTurn(yaw + sign * Angles.DegToRad(rng.Range(p.ScanAngleMinDeg, p.ScanAngleMaxDeg)), allowHop: state != SparrowState.Perched);

        // 振り向いたあと、ときどき首をかしげてしばらく見つめる
        if (rng.Chance(p.HeadTiltChance))
        {
            headTilt = (rng.Chance(0.5f) ? 1f : -1f) * Angles.DegToRad(rng.Range(p.HeadTiltMinDeg, p.HeadTiltMaxDeg));
            headTiltHold = rng.Range(p.HeadTiltHoldMin, p.HeadTiltHoldMax);
        }
        scanCountdown = rng.Range(p.ScanIntervalMin, p.ScanIntervalMax);
    }

    // ---- Hop / Alarm ----

    private void EnterHop(in SparrowInput input)
    {
        SetState(SparrowState.Hop);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        hopsLeft = rng.RangeInt(p.HopsPerBoutMin, p.HopsPerBoutMax);
        StartHop(input);
    }

    private void EnterAlarm(in SparrowInput input)
    {
        SetState(SparrowState.Alarm);
        anim = SparrowAnim.Idle;
        pitch = 0f;
        triggers |= SparrowTrigger.Alarm;
        alarmCooldown = p.AlarmCooldown;
        fleeDirection = Vector3.Normalize(Angles.Horizontal(position - input.PlayerPosition));
        hopsLeft = rng.RangeInt(p.AlarmHopsMin, p.AlarmHopsMax);
        StartHop(input);
    }

    private void StartHop(in SparrowInput input)
    {
        var distance = rng.Range(p.HopDistanceMin, p.HopDistanceMax);
        Vector3 to;
        if (state == SparrowState.Alarm)
        {
            to = position + fleeDirection * distance;
        }
        else
        {
            var delta = Angles.Horizontal(goal - position);
            var gap = delta.Length();
            // 1 回で届く距離なら目標に直接降りる(段差の途中で止まらないように)
            var share = gap <= p.HopDistanceMax ? 1f : distance / gap;
            to = position + delta * share;
            // 1 回で上り下りする高さには限りがある。大きな段差は数回に分けるか、飛んで越える
            to.Y = position.Y + Math.Clamp((goal.Y - position.Y) * share, -p.MaxHopRise, p.MaxHopRise);
        }

        // 着地点の地面が分かれば、その高さに降りる。1 回で越えられない段差は飛んで越える
        if (groundAt?.Invoke(to) is { } landing)
        {
            if (MathF.Abs(landing - position.Y) > p.MaxHopRise)
            {
                EnterTakeOff(input);
                return;
            }

            to.Y = landing;
        }

        // 跳びながら、最初のうちに跳ぶ方向へ向きを変える
        if (Angles.HorizontalDistance(position, to) > Epsilon)
            BeginTurn(Angles.YawTowards(position, to), p.HopDuration * p.HopTurnShare);

        hop = new Hop(position, to, rng.Range(p.HopHeightMin, p.HopHeightMax), p.HopDuration);
        hopTime = 0f;
    }

    private void TickHop(in SparrowInput input, float dt)
    {
        if (hop is { } current)
        {
            hopTime += dt;
            position = current.Evaluate(hopTime);
            if (hopTime < current.Duration)
                return;

            position = current.To;
            hop = null;
            hopsLeft--;
            FlickTail(p.HopTailFlickDeg, p.HopTailFlickDuration);
            hopGapLeft = rng.Range(p.HopGapMin, p.HopGapMax);
            return;
        }

        hopGapLeft -= dt;
        if (hopGapLeft > 0f)
            return;

        var fleeing = state == SparrowState.Alarm;
        if (fleeing)
        {
            if (ShouldFly(input))
            {
                EnterTakeOff(input);
                return;
            }
        }
        else if (TryInterrupt(input))
        {
            return;
        }

        if (hopsLeft > 0 && (fleeing || NeedsToMove(input)))
            StartHop(input);
        else
            EnterIdle();
    }

    // ---- Forage / Preen ----

    private void EnterForage()
    {
        SetState(SparrowState.Forage);
        pecks = rng.RangeInt(p.PecksMin, p.PecksMax);
    }

    private void TickForage(in SparrowInput input)
    {
        if (TryInterrupt(input))
            return;

        if (stateTime >= pecks * p.PeckDuration)
        {
            EnterIdle();
            return;
        }

        // ついばみ: 1 回ごとに頭を下げて戻す
        var u = stateTime % p.PeckDuration / p.PeckDuration;
        pitch = Angles.DegToRad(p.PeckPitchDeg) * MathF.Sin(MathF.PI * u);
    }

    private void EnterPreen()
    {
        preenRequested = false;
        SetState(SparrowState.Preen);
        CancelTurn();
        stateDuration = rng.Range(p.PreenDurationMin, p.PreenDurationMax);
        preenSide = rng.Chance(0.5f) ? 1f : -1f;
        preenWing = rng.Chance(p.PreenWingChance);
        // 背中の手入れでは、途中で反対側に替えることがある
        preenSwitchAt = !preenWing && rng.Chance(p.PreenSwitchSideChance) ? stateDuration * 0.5f : float.MaxValue;
    }

    private void TickPreen(in SparrowInput input)
    {
        if (TryInterrupt(input))
            return;

        if (stateTime >= stateDuration)
        {
            EnterIdle();
            return;
        }

        if (stateTime >= preenSwitchAt)
        {
            preenSide = -preenSide;
            preenSwitchAt = float.MaxValue;
        }

        // 首を後ろへひねり、くちばしで細かくつつく。体は手入れする側へ少し傾ける
        neckNibble = Angles.DegToRad(p.PreenNibbleDeg) * MathF.Sin(MathF.Tau * p.PreenNibbleHz * stateTime);
        pitch = 0f;
        roll = preenSide * Angles.DegToRad(p.PreenBodyRollDeg);
        if (preenWing)
        {
            // 翼の手入れ: その側の翼を持ち上げて開き、翼の内側へ首を深く入れる
            neckYawTarget = preenSide * Angles.DegToRad(p.PreenWingNeckYawDeg);
            neckPitchTarget = Angles.DegToRad(p.PreenWingNeckPitchDeg);
            wingSpread = Angles.DegToRad(p.PreenWingLiftDeg);
            wingFlap = 0f;
            wingSide = preenSide;
        }
        else
        {
            neckYawTarget = preenSide * Angles.DegToRad(p.PreenBackNeckYawDeg);
            neckPitchTarget = Angles.DegToRad(p.PreenBackNeckPitchDeg);
            wingSpread = 0f;
        }
    }

    // ---- DustBath ----

    /// <summary>プレイヤーが止まっている間、次の砂浴びまでの時間を数える。動き出したら数え直し。</summary>
    private void UpdateDustBathTimer(float dt)
    {
        if (playerStillTime < p.DustBathDelay || float.IsPositiveInfinity(p.DustBathIntervalMin))
        {
            dustBathArmed = false;
            return;
        }

        if (!dustBathArmed)
        {
            dustBathArmed = true;
            dustBathCountdown = rng.Range(p.DustBathIntervalMin, p.DustBathIntervalMax);
            return;
        }

        dustBathCountdown -= dt;
    }

    private void EnterDustBath()
    {
        // 終わったら次の間隔を数え直す
        dustBathArmed = false;
        dustBathRequested = false;
        SetState(SparrowState.DustBath);
        CancelTurn();
        dustBaseYaw = yaw;
        stateDuration = rng.Range(p.DustBathDurationMin, p.DustBathDurationMax);
        shaking = false;
        StartDustPhase(fluttering: true);
    }

    private void StartDustPhase(bool fluttering)
    {
        dustFluttering = fluttering;
        dustPhaseLeft = fluttering
            ? rng.Range(p.DustFlutterMin, p.DustFlutterMax)
            : rng.Range(p.DustPauseMin, p.DustPauseMax);
        if (fluttering)
            triggers |= SparrowTrigger.DustFlutter;
    }

    private void TickDustBath(in SparrowInput input, float dt)
    {
        if (TryInterrupt(input))
            return;

        if (shaking)
        {
            // 最後に立ち上がって体をブルッと振るう
            shakeTime += dt;
            roll = Angles.DegToRad(p.ShakeRollDeg) * MathF.Sin(MathF.Tau * p.ShakeHz * shakeTime);
            if (shakeTime >= p.ShakeDuration)
                EnterIdle();
            return;
        }

        if (stateTime >= stateDuration)
        {
            shaking = true;
            shakeTime = 0f;
            anim = SparrowAnim.Idle;
            crouch = 0f;
            pitch = 0f;
            wingSpread = 0f;
            wingFlap = 0f;
            yaw = dustBaseYaw;
            return;
        }

        dustPhaseLeft -= dt;
        if (dustPhaseLeft <= 0f)
            StartDustPhase(!dustFluttering);

        // 腹を地面に付けたまま (止まり姿勢)、ばたつきの間は翼を高速でばたつかせ、体を地面に擦りつける
        anim = SparrowAnim.Idle;
        crouch = p.DustCrouch;
        if (dustFluttering)
        {
            var t = stateTime;
            wingSpread = Angles.DegToRad(p.DustWingSpreadDeg);
            wingFlap = Angles.DegToRad(p.DustWingBeatDeg) * MathF.Sin(MathF.Tau * p.DustWingBeatHz * t);
            pitch = Angles.DegToRad(p.DustPitchDeg) + Angles.DegToRad(p.DustRockPitchDeg) * MathF.Sin(MathF.Tau * p.DustRockHz * t);
            roll = Angles.DegToRad(p.DustRollDeg) * MathF.Sin(MathF.Tau * p.DustRollHz * t);
            yaw = dustBaseYaw + Angles.DegToRad(p.DustShimmyYawDeg) * MathF.Sin(MathF.Tau * p.DustShimmyHz * t);
        }
        else
        {
            wingSpread = 0f;
            wingFlap = 0f;
            pitch = Angles.DegToRad(p.DustPitchDeg) * 0.5f;
            roll = 0f;
            yaw = dustBaseYaw;
        }
    }

    // ---- TakeOff / Fly / Land ----

    private void EnterTakeOff(in SparrowInput input)
    {
        SetState(SparrowState.TakeOff);
        anim = SparrowAnim.Fly;
        pitch = 0f;
        hop = null;
        triggers |= SparrowTrigger.TakeOff;
        groundY = position.Y;
        altitude = 0f;
        CancelTurn();
        if (Angles.HorizontalDistance(position, goal) > Epsilon)
            BeginTurn(Angles.YawTowards(position, goal), p.TakeOffDuration);
    }

    private void TickTakeOff(in SparrowInput input, float dt)
    {
        altitude += p.TakeOffClimbSpeed * dt;
        MoveTowards(goal, p.FlySpeed * 0.5f * dt, dt);
        AvoidTerrain(dt);
        position.Y = groundY + altitude;
        KeepAboveGround(0f);
        if (stateTime < p.TakeOffDuration)
            return;

        SetState(SparrowState.Fly);
        flapping = true;
        flightPhaseLeft = p.FlapDuration;
    }

    private void TickFly(in SparrowInput input, float dt)
    {
        var gap = Angles.HorizontalDistance(position, goal);
        var playerSlow = Angles.Horizontal(input.PlayerVelocity).Length() < p.LandPlayerSpeed;

        float speed;
        if (playerSlow && gap < p.ApproachDistance)
        {
            // 着地前の滑空: 羽ばたきの上下をやめ、目標までの距離に比例した高さへ下りながら減速する
            var glideAltitude = MathF.Min(altitude, p.GlideSlope * gap);
            altitude = MathF.Max(glideAltitude, altitude - p.GlideDescentSpeed * dt);
            speed = Math.Clamp(gap * p.ApproachSpeedPerYalm, p.ApproachMinSpeed, p.FlySpeed);
        }
        else
        {
            // 波状飛行: 羽ばたいて上昇 → 翼を畳んで滑空・下降 を繰り返す
            flightPhaseLeft -= dt;
            if (flightPhaseLeft <= 0f)
            {
                flapping = !flapping;
                flightPhaseLeft = flapping ? p.FlapDuration : p.GlideDuration;
            }

            altitude += (flapping ? p.FlapClimbSpeed : -p.GlideSinkSpeed) * dt;
            altitude = Math.Clamp(altitude, p.FlyAltitudeMin, p.FlyAltitudeMax);

            // 騎乗中などで論理位置が速く動くときは、それより速く飛ぶ
            speed = MathF.Max(p.FlySpeed, Angles.Horizontal(input.PlayerVelocity).Length() * 1.3f);
        }

        MoveTowards(goal, speed * dt, dt);
        AvoidTerrain(dt);
        position.Y = groundY + altitude;
        KeepAboveGround(p.FlightClearance);

        // プレイヤーが速く動いている間は降りずに付いていく。地面の高さが目標に追いつくまでと、
        // 滑空の高さまで下りきるまでは降りない(真上から急降下しない)
        if (playerSlow
            && Angles.HorizontalDistance(position, goal) < p.LandDistance
            && MathF.Abs(goal.Y - groundY) < Epsilon
            && altitude <= p.GlideSlope * p.LandDistance + Epsilon)
        {
            SetState(SparrowState.Land);
            landStartAltitude = altitude;
            landStartSpeed = speed;
            landStartPosition = position;
        }
    }

    private void TickLand(in SparrowInput input, float dt)
    {
        // フレア: 前進も降下もなめらかに 0 へ近づけ、頭を少し上げてブレーキをかける。
        // 目標の手前で接地し、残りはホップで詰める
        var u = MathF.Min(1f, stateTime / p.LandDuration);
        var settled = u * u * (3f - 2f * u);
        pitch = -Angles.DegToRad(p.FlarePitchDeg) * MathF.Sin(MathF.PI * u);

        if (perchIntent)
        {
            // 頭の上にはぴったり降りる(頭は歩いて動くことがある)
            position = Vector3.Lerp(landStartPosition, goal, settled);
            if (u >= 1f)
            {
                EnterPerched();
                FlickTail(p.LandTailFlickDeg, p.LandTailFlickDuration);
            }
            return;
        }

        altitude = landStartAltitude * (1f - settled);
        MoveTowards(goal, landStartSpeed * (1f - u) * dt, dt);
        // 降りる場所の実際の地面へ近づける
        if (groundAt?.Invoke(position) is { } under)
            groundY = MoveToward(groundY, under, p.GroundFollowSpeed * dt);
        position.Y = groundY + altitude;
        KeepAboveGround(0f);

        if (u >= 1f)
        {
            returning = false;
            EnterIdle(afterLanding: true);
            FlickTail(p.LandTailFlickDeg, p.LandTailFlickDuration);
        }
    }

    /// <summary>真下と少し先の地形が基準の地面より高ければ、基準を持ち上げて越える。</summary>
    private void AvoidTerrain(float dt)
    {
        if (groundAt == null)
            return;

        // 先を見るのは行き先まで(行き先より奥の地形で基準が上がり、降りられなくならないように)
        var forward = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var lookAhead = MathF.Min(p.TerrainLookAhead, Angles.HorizontalDistance(position, goal));
        var terrain = MathF.Max(
            groundAt(position) ?? float.MinValue,
            groundAt(position + forward * lookAhead) ?? float.MinValue);
        if (terrain > groundY)
            groundY = MoveToward(groundY, terrain, p.TerrainClimbSpeed * dt);
    }

    /// <summary>真下の地面より clearance 以上、上にいるようにする(地面に埋まらない)。</summary>
    private void KeepAboveGround(float clearance)
    {
        if (groundAt?.Invoke(position) is { } under && position.Y < under + clearance)
            position.Y = under + clearance;
    }

    private static float MoveToward(float from, float to, float maxStep) =>
        from + Math.Clamp(to - from, -maxStep, maxStep);

    /// <summary>
    /// 水平に step だけ進む。地面の高さの基準も、進んだ割合だけ目標へ近づける(一度に動く量は GroundFollowSpeed まで)。
    /// </summary>
    private void MoveTowards(Vector3 target, float step, float dt)
    {
        var delta = Angles.Horizontal(target - position);
        var gap = delta.Length();
        var share = gap <= step ? 1f : step / gap;
        var maxGroundChange = p.GroundFollowSpeed * dt;
        groundY += Math.Clamp((target.Y - groundY) * share, -maxGroundChange, maxGroundChange);
        if (gap <= Epsilon)
            return;

        // 飛んでいる間は、向きを変える速さに上限を付ける(飛び立ちの向き変えが続いていればそれに任せる)
        if (!turning)
        {
            var maxTurn = Angles.DegToRad(p.FlyTurnRateDeg) * dt;
            yaw += Math.Clamp(Angles.Wrap(Angles.YawTowards(position, target) - yaw), -maxTurn, maxTurn);
        }

        position += delta / gap * MathF.Min(step, gap);
    }
}
