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

    // プレイヤーの周りを動き回る: プレイヤーが止まってこの秒数たったら、周りの地点を渡り歩く
    public float WanderDelay { get; set; } = 2f;
    public float WanderIntervalMin { get; set; } = 3f;
    public float WanderIntervalMax { get; set; } = 8f;
    public float WanderRadiusMin { get; set; } = 0.8f;
    public float WanderRadiusMax { get; set; } = 2.5f;
    // これより遅ければプレイヤーは止まっているとみなす
    public float StillSpeed { get; set; } = 0.3f;

    // 採餌・羽繕い
    public float ForageChance { get; set; } = 0.15f;
    public float PreenChance { get; set; } = 0.03f;
    public int PecksMin { get; set; } = 2;
    public int PecksMax { get; set; } = 5;
    public float PeckDuration { get; set; } = 0.15f;
    public float PeckPitchDeg { get; set; } = 35f;
    public float PreenDurationMin { get; set; } = 1.0f;
    public float PreenDurationMax { get; set; } = 2.0f;

    // 砂浴び: プレイヤーがこの秒数止まっていたら、そこから 20〜60 秒ごとに、落ち着いたところで始める
    public float DustBathDelay { get; set; } = 3f;
    public float DustBathIntervalMin { get; set; } = 20f;
    public float DustBathIntervalMax { get; set; } = 60f;
    public float DustBathDurationMin { get; set; } = 4f;
    public float DustBathDurationMax { get; set; } = 8f;
    // ばたつきと小休止の長さ
    public float DustFlutterMin { get; set; } = 0.5f;
    public float DustFlutterMax { get; set; } = 1.0f;
    public float DustPauseMin { get; set; } = 0.3f;
    public float DustPauseMax { get; set; } = 0.8f;
    // 腹ばいで沈む深さ (yalm)、前傾、左右の揺れ
    public float DustCrouch { get; set; } = 0.03f;
    public float DustPitchDeg { get; set; } = 20f;
    public float DustRollDeg { get; set; } = 15f;
    public float DustRollHz { get; set; } = 6f;
    // 終わりに体を振るう
    public float ShakeDuration { get; set; } = 0.3f;
    public float ShakeRollDeg { get; set; } = 20f;
    public float ShakeHz { get; set; } = 10f;

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
    // 着地前の滑空: プレイヤーが止まり、この距離まで来たら羽ばたきをやめて下り坂で近づく
    public float ApproachDistance { get; set; } = 4f;
    // 滑空の下り坂 (目標までの水平距離 1y あたりの高さ)
    public float GlideSlope { get; set; } = 0.35f;
    public float GlideDescentSpeed { get; set; } = 1.5f;
    // 滑空中の速さ = 目標までの距離 × この値 (最低 ApproachMinSpeed)
    public float ApproachSpeedPerYalm { get; set; } = 2.5f;
    public float ApproachMinSpeed { get; set; } = 1.5f;
    // フレア: 最後に減速しながら頭を上げて降りる
    public float LandDuration { get; set; } = 0.35f;
    public float FlarePitchDeg { get; set; } = 15f;
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
