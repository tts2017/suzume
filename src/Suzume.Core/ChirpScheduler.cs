namespace Suzume.Core;

/// <summary>行動に合わせて、いつ・どの鳴き声を鳴らすかを決める。鳴きすぎないよう最小間隔で間引く。</summary>
public sealed class ChirpScheduler
{
    private readonly SparrowParams p;
    private readonly IRandom rng;
    private float cooldown;

    public ChirpScheduler(SparrowParams parameters, IRandom random)
    {
        p = parameters;
        rng = random;
    }

    public ChirpKind? Update(float dt, SparrowState state, SparrowTrigger triggers)
    {
        cooldown = MathF.Max(0f, cooldown - dt);

        // 行動に結びついた鳴き声は間隔に関係なく鳴らす
        if (triggers.HasFlag(SparrowTrigger.Alarm))
            return Emit(ChirpKind.Alarm);
        if (triggers.HasFlag(SparrowTrigger.TakeOff))
            return Emit(ChirpKind.Flight);
        // 砂浴びの羽音は鳴き声ではないので、間隔に関係なく鳴らし、間隔も使わない
        if (triggers.HasFlag(SparrowTrigger.DustFlutter))
            return ChirpKind.Dust;

        if (cooldown > 0f || p.ChirpFrequency <= 0f)
            return null;

        if (triggers.HasFlag(SparrowTrigger.LongIdleStart)
            && rng.Chance(MathF.Min(1f, p.SongChance * p.ChirpFrequency)))
            return Emit(ChirpKind.Song);

        if (state is SparrowState.Idle or SparrowState.Hop or SparrowState.Forage or SparrowState.Perched)
        {
            var chance = 1f - MathF.Exp(-p.ContactRatePerSecond * p.ChirpFrequency * dt);
            if (rng.Chance(chance))
                return Emit(ChirpKind.Contact);
        }

        return null;
    }

    private ChirpKind Emit(ChirpKind kind)
    {
        cooldown = p.ChirpMinInterval;
        return kind;
    }
}
