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
}

/// <summary>ゲーム側で再生させるベースアニメーション。</summary>
public enum SparrowAnim
{
    Idle,
    Fly,
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
}

/// <summary>その Tick で起きた出来事。鳴き声のきっかけに使う。</summary>
[Flags]
public enum SparrowTrigger
{
    None = 0,
    TakeOff = 1,
    Alarm = 2,
    LongIdleStart = 4,
}

/// <param name="Target">ゲームの AI が決めたミニオンの論理位置(地面の高さ込み)</param>
/// <param name="TargetYaw">論理位置での向き。置き直すときに使う</param>
public readonly record struct SparrowInput(
    float Dt,
    Vector3 Target,
    float TargetYaw,
    Vector3 PlayerPosition,
    Vector3 PlayerVelocity,
    bool PlayerMounted);

/// <param name="Position">見た目の位置</param>
/// <param name="Pitch">前傾角(ラジアン、正で頭が下がる)</param>
public readonly record struct SparrowPose(
    Vector3 Position,
    float Yaw,
    float Pitch,
    SparrowAnim Anim,
    SparrowState State,
    SparrowTrigger Triggers);
