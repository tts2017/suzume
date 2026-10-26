using System.Numerics;

namespace Suzume.Core;

/// <summary>
/// スズメが付いていく地上の目標地点。プレイヤーの斜め後ろの足元。
/// タビスズメの論理位置は宙に浮いていたり頭の上だったりするため、プレイヤーから求める。
/// </summary>
public static class FollowTarget
{
    public const float SideOffset = 0.9f;
    public const float BackOffset = 1.1f;

    public static Vector3 For(Vector3 playerPosition, float playerYaw)
    {
        var local = new Vector3(SideOffset, 0f, -BackOffset);
        return playerPosition + Vector3.Transform(local, Quaternion.CreateFromAxisAngle(Vector3.UnitY, playerYaw));
    }
}

/// <summary>地面の高さ。プレイヤーの足元を使い、ジャンプ中は直前の地面の高さを保つ。</summary>
public sealed class GroundTracker
{
    private float? lastGround;

    public float Update(float playerY, bool jumping)
    {
        if (jumping && lastGround is { } held)
            return held;

        if (!jumping)
            lastGround = playerY;
        return playerY;
    }

    public void Reset() => lastGround = null;
}
