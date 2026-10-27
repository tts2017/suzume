using System.Numerics;

namespace Suzume.Core;

public enum SparrowState
{
    Idle,
    Hop,
    Forage,
    Preen,
    TakeOff,
    Fly,
    Land,
    Alarm,
    /// <summary>プレイヤーの頭の上に止まっている</summary>
    Perched,
    /// <summary>砂浴び</summary>
    DustBath,
}

/// <summary>ゲーム側で再生させるベースアニメーション。</summary>
public enum SparrowAnim
{
    Idle,
    Fly,
    /// <summary>その場で羽をばたつかせる(砂浴び)</summary>
    Flutter,
}

public enum ChirpKind
{
    /// <summary>「チュン」地鳴き</summary>
    Contact,
    /// <summary>「チュンチュン」囀り</summary>
    Song,
    /// <summary>「ジュクジュク」警戒</summary>
    Alarm,
    /// <summary>「チュリリ」飛び立ち</summary>
    Flight,
    /// <summary>「バサバサ」砂浴びの羽音(鳴き声ではない)</summary>
    Dust,
}

/// <summary>その Tick で起きた出来事。鳴き声のきっかけに使う。</summary>
[Flags]
public enum SparrowTrigger
{
    None = 0,
    TakeOff = 1,
    Alarm = 2,
    LongIdleStart = 4,
    /// <summary>砂浴びでばたつき始めた</summary>
    DustFlutter = 8,
}

/// <param name="Target">スズメが付いていく地上の目標地点(地面の高さ込み)</param>
/// <param name="TargetYaw">論理位置での向き。置き直すときに使う</param>
/// <param name="HeadPosition">プレイヤーの頭の上(止まる位置)</param>
/// <param name="PerchRequested">この Tick で手招きされた(頭に乗る/下りるの切り替え)</param>
/// <param name="Threatened">この Tick でスズメに向けて攻撃的なエモートをされた</param>
public readonly record struct SparrowInput(
    float Dt,
    Vector3 Target,
    float TargetYaw,
    Vector3 PlayerPosition,
    Vector3 PlayerVelocity,
    bool PlayerMounted,
    Vector3 HeadPosition = default,
    bool PerchRequested = false,
    bool PlayerJumping = false,
    bool Threatened = false);

/// <param name="Position">見た目の位置</param>
/// <param name="Pitch">前傾角(ラジアン、正で頭が下がる)</param>
/// <param name="Roll">左右の傾き(ラジアン)</param>
public readonly record struct SparrowPose(
    Vector3 Position,
    float Yaw,
    float Pitch,
    SparrowAnim Anim,
    SparrowState State,
    SparrowTrigger Triggers,
    float Roll = 0f);
