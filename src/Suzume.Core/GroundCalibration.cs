namespace Suzume.Core;

/// <summary>
/// 地形の当たり判定 (レイキャスト) は見た目の地面より少し低い。キャラクターが当たり判定から
/// どれだけ浮いて立っているかをプレイヤーで測り、レイキャストの結果に足して見た目の地面にそろえる。
/// </summary>
public sealed class GroundCalibration
{
    // これより大きい差は測り間違い(足元の下に別の床がある等)とみなす
    private const float MaxPlausible = 0.5f;
    // 1 回の測定で差の何割を取り入れるか(急に変わらないように)
    private const float Smoothing = 0.1f;

    private float offset;

    public void Update(float playerY, float? groundUnderPlayer, bool grounded)
    {
        if (!grounded || groundUnderPlayer is not { } ground)
            return;

        var difference = playerY - ground;
        if (MathF.Abs(difference) > MaxPlausible)
            return;

        offset += (difference - offset) * Smoothing;
    }

    public float Apply(float collisionY) => collisionY + offset;
}
