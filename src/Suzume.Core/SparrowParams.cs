namespace Suzume.Core;

/// <summary>スズメの行動パラメータ。距離は yalm、時間は秒、角度は度。</summary>
public sealed class SparrowParams
{
    // ホップ
    public float HopDuration { get; set; } = 0.12f;
    public float HopDistanceMin { get; set; } = 0.15f;
    public float HopDistanceMax { get; set; } = 0.30f;
    public float HopHeightMin { get; set; } = 0.06f;
    public float HopHeightMax { get; set; } = 0.10f;
    public float HopGapMin { get; set; } = 0.05f;
    public float HopGapMax { get; set; } = 0.15f;
    public int HopsPerBoutMin { get; set; } = 2;
    public int HopsPerBoutMax { get; set; } = 6;

    // 待機・キョロキョロ
    public float IdleMin { get; set; } = 0.3f;
    public float IdleMax { get; set; } = 1.5f;
    public float LongIdleThreshold { get; set; } = 1.0f;
    public float ScanIntervalMin { get; set; } = 0.5f;
    public float ScanIntervalMax { get; set; } = 1.0f;
    public float ScanAngleMinDeg { get; set; } = 20f;
    public float ScanAngleMaxDeg { get; set; } = 70f;
    public float ArriveDistance { get; set; } = 0.4f;
    // この高低差を超えたら、その場で止まらずにホップで合わせる
    public float StepHeight { get; set; } = 0.05f;
    // 1 回のホップで上り下りできる高さ
    public float MaxHopRise { get; set; } = 0.2f;

    // 採餌・羽繕い
    public float ForageChance { get; set; } = 0.15f;
    public float PreenChance { get; set; } = 0.03f;
    public int PecksMin { get; set; } = 2;
    public int PecksMax { get; set; } = 5;
    public float PeckDuration { get; set; } = 0.15f;
    public float PeckPitchDeg { get; set; } = 35f;
    public float PreenDurationMin { get; set; } = 1.0f;
    public float PreenDurationMax { get; set; } = 2.0f;

    // 飛行
    public float FlyDistance { get; set; } = 6f;
    public float FastFlyDistance { get; set; } = 1.5f;
    public float SprintSpeed { get; set; } = 7.5f;
    // この高低差を超えたら、ホップではなく飛んで上り下りする
    public float FlyHeightTrigger { get; set; } = 0.5f;
    public float TakeOffDuration { get; set; } = 0.25f;
    public float TakeOffClimbSpeed { get; set; } = 2f;
    public float FlySpeed { get; set; } = 8f;
    public float FlapDuration { get; set; } = 0.3f;
    public float GlideDuration { get; set; } = 0.2f;
    public float FlapClimbSpeed { get; set; } = 1.2f;
    public float GlideSinkSpeed { get; set; } = 1.0f;
    public float FlyAltitudeMin { get; set; } = 0.5f;
    public float FlyAltitudeMax { get; set; } = 2.0f;
    public float LandDistance { get; set; } = 1.0f;
    // プレイヤーがこれより速く動いている間は着地せず、飛んだまま付いていく
    public float LandPlayerSpeed { get; set; } = 3f;
    // 飛行中に地面の高さの基準を目標へ合わせる速さ (yalm/秒)
    public float GroundFollowSpeed { get; set; } = 3f;
    public float LandDuration { get; set; } = 0.25f;
    public float LandSettleDuration { get; set; } = 0.15f;
    public float LandPitchDeg { get; set; } = 15f;
    public float ResetDistance { get; set; } = 30f;

    // 警戒
    public float AlarmDistance { get; set; } = 2f;
    public float AlarmPlayerSpeed { get; set; } = 5f;
    public float AlarmCooldown { get; set; } = 10f;
    public int AlarmHopsMin { get; set; } = 1;
    public int AlarmHopsMax { get; set; } = 3;

    // 鳴き声
    public float ChirpMinInterval { get; set; } = 1.5f;
    public float ChirpFrequency { get; set; } = 1.0f;
    public float ContactRatePerSecond { get; set; } = 0.15f;
    public float SongChance { get; set; } = 0.3f;
}
