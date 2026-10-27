namespace Suzume.Core;

/// <summary>
/// 冬のスズメが羽毛をふくらませた「ふくら雀」の形にするための、ボーンごとの大きさ。
/// 体 (腰・背) を大きく、頭は少しだけ大きくし、くちばし・翼・尾・脚は元の大きさのままにする。
/// 骨格の大きさは親から子へ掛け合わされるので、子では親の分を割り戻す。
/// </summary>
public static class PuffScale
{
    // 頭のふくらみは体の何割か
    private const float HeadShare = 0.3f;

    /// <summary>このボーンのモデル空間での大きさ。</summary>
    public static float Target(string? bone, float amount) => bone switch
    {
        "j_kosi" or "j_sebo" => 1f + amount,
        "j_kubi" => 1f + amount * HeadShare,
        _ => 1f,
    };

    /// <summary>各ボーンの局所の大きさに掛ける倍率。</summary>
    /// <param name="names">ボーン名 (分からなければ null)</param>
    /// <param name="parents">親のボーン番号 (根は負)</param>
    /// <param name="amount">体をどれだけ大きくするか (0.15 で 15%)</param>
    public static float[] LocalFactors(IReadOnlyList<string?> names, IReadOnlyList<int> parents, float amount)
    {
        var factors = new float[names.Count];
        for (var bone = 0; bone < names.Count; bone++)
        {
            var parent = parents[bone];
            var parentTarget = parent >= 0 ? Target(names[parent], amount) : 1f;
            factors[bone] = Target(names[bone], amount) / parentTarget;
        }

        return factors;
    }
}
