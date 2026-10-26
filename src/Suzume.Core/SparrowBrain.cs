using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// 実際のスズメ(地上は両足ホップ、遠くへは波状飛行)を真似る状態機械。
/// ゲームの論理位置 (Target) を、ホップや飛行で追いかける「見た目の位置」を計算する。
/// </summary>
public sealed class SparrowBrain
{
    // ロード明けなどの巨大なフレーム時間で瞬間移動しないように抑える
    private const float MaxDt = 0.1f;
    private const float Epsilon = 1e-3f;

    private readonly SparrowParams p;
    private readonly IRandom rng;

    private bool initialized;
    private Vector3 position;
    private float yaw;
    private float pitch;
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

    // Forage / Preen
    private int pecks;
    private float preenBaseYaw;

    // TakeOff / Fly / Land
    private bool flapping;
    private float flightPhaseLeft;
    private float groundY;
    private float altitude;
    private float landStartAltitude;

    private float alarmCooldown;

    public SparrowBrain(SparrowParams parameters, IRandom random)
    {
        p = parameters;
        rng = random;
    }

    /// <summary>次の Tick で論理位置へ置き直す(ミニオンの再召喚・一時停止明け)。</summary>
    public void Invalidate() => initialized = false;

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
                TickTakeOff(dt);
                break;
            case SparrowState.Fly:
                TickFly(input, dt);
                break;
            case SparrowState.Land:
                TickLand(input, dt);
                break;
        }

        yaw = Angles.Wrap(yaw);
        return CurrentPose();
    }

    private SparrowPose CurrentPose() => new(position, yaw, pitch, anim, state, triggers);

    private void Reset(in SparrowInput input)
    {
        initialized = true;
        position = input.Target;
        yaw = Angles.Wrap(input.TargetYaw);
        hop = null;
        alarmCooldown = 0f;
        scanCountdown = rng.Range(p.ScanIntervalMin, p.ScanIntervalMax);
        EnterIdle();
    }

    private void SetState(SparrowState next)
    {
        state = next;
        stateTime = 0f;
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
        var gap = Angles.HorizontalDistance(position, input.Target);
        var fast = input.PlayerMounted || Angles.Horizontal(input.PlayerVelocity).Length() > p.SprintSpeed;
        return gap > p.FlyDistance
            || (fast && gap > p.FastFlyDistance)
            || MathF.Abs(input.Target.Y - position.Y) > p.FlyHeightTrigger;
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
        Angles.HorizontalDistance(position, input.Target) > p.ArriveDistance
        || MathF.Abs(input.Target.Y - position.Y) > p.StepHeight;

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

        scanCountdown -= dt;
        if (scanCountdown <= 0f)
        {
            // キョロキョロ: 補間せず一瞬で向きを変える
            var sign = rng.Chance(0.5f) ? 1f : -1f;
            yaw += sign * Angles.DegToRad(rng.Range(p.ScanAngleMinDeg, p.ScanAngleMaxDeg));
            scanCountdown = rng.Range(p.ScanIntervalMin, p.ScanIntervalMax);
        }

        if (stateTime < stateDuration)
            return;

        if (NeedsToMove(input))
            EnterHop(input);
        else if (rng.Chance(p.ForageChance))
            EnterForage();
        else if (rng.Chance(p.PreenChance))
            EnterPreen();
        else
            EnterIdle();
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
            var delta = Angles.Horizontal(input.Target - position);
            var gap = delta.Length();
            // 1 回で届く距離なら目標に直接降りる(段差の途中で止まらないように)
            if (gap <= p.HopDistanceMax)
            {
                to = input.Target;
            }
            else
            {
                to = position + delta / gap * distance;
                to.Y = position.Y + (input.Target.Y - position.Y) * (distance / gap);
            }
        }

        // 向きはホップの前に一瞬で変える。跳んでいる間は変えない
        if (Angles.HorizontalDistance(position, to) > Epsilon)
            yaw = Angles.YawTowards(position, to);

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
        SetState(SparrowState.Preen);
        stateDuration = rng.Range(p.PreenDurationMin, p.PreenDurationMax);
        preenBaseYaw = yaw;
    }

    private void TickPreen(in SparrowInput input)
    {
        if (TryInterrupt(input))
            return;

        if (stateTime >= stateDuration)
        {
            yaw = preenBaseYaw;
            EnterIdle();
            return;
        }

        // 羽繕いの代用: 小刻みに体を揺らす(v2 でボーン操作に置き換える)
        pitch = Angles.DegToRad(10f) * MathF.Sin(stateTime * MathF.Tau * 3f);
        yaw = preenBaseYaw + 0.2f * MathF.Sin(stateTime * MathF.Tau * 1.5f);
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
        if (Angles.HorizontalDistance(position, input.Target) > Epsilon)
            yaw = Angles.YawTowards(position, input.Target);
    }

    private void TickTakeOff(float dt)
    {
        altitude += p.TakeOffClimbSpeed * dt;
        position.Y = groundY + altitude;
        if (stateTime < p.TakeOffDuration)
            return;

        SetState(SparrowState.Fly);
        flapping = true;
        flightPhaseLeft = p.FlapDuration;
    }

    private void TickFly(in SparrowInput input, float dt)
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
        var speed = MathF.Max(p.FlySpeed, Angles.Horizontal(input.PlayerVelocity).Length() * 1.3f);
        MoveHorizontallyTowards(input.Target, speed * dt);
        position.Y = groundY + altitude;

        if (Angles.HorizontalDistance(position, input.Target) < p.LandDistance)
        {
            SetState(SparrowState.Land);
            landStartAltitude = altitude;
        }
    }

    private void TickLand(in SparrowInput input, float dt)
    {
        var u = MathF.Min(1f, stateTime / p.LandDuration);
        altitude = landStartAltitude * (1f - u);
        MoveHorizontallyTowards(input.Target, p.FlySpeed * 0.5f * dt);
        position.Y = groundY + altitude;

        if (u >= 1f)
            EnterIdle(afterLanding: true);
    }

    /// <summary>水平に step だけ進み、地面の高さも進んだ割合だけ目標に近づける。</summary>
    private void MoveHorizontallyTowards(Vector3 target, float step)
    {
        var delta = Angles.Horizontal(target - position);
        var gap = delta.Length();
        if (gap <= Epsilon)
            return;

        yaw = Angles.YawTowards(position, target);
        step = MathF.Min(step, gap);
        groundY += (target.Y - groundY) * (step / gap);
        position += delta / gap * step;
    }
}
