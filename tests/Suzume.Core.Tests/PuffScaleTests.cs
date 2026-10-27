using Suzume.Core;

namespace Suzume.Core.Tests;

public class PuffScaleTests
{
    // タビスズメに似せた骨格: 腰 → 背 → 首 → くちばし、背 → 翼、腰 → 尾・脚
    private static readonly string?[] Names =
        ["n_root", "j_kosi", "j_sebo", "j_kubi", "j_mouth", "j_ude_a_l", "j_ude_b_l", "j_tail", "j_asi_a_l", "j_asi_b_l"];
    private static readonly int[] Parents = [-1, 0, 1, 2, 3, 2, 5, 1, 1, 8];

    // 局所の倍率を根から掛けていった、モデル空間での大きさ
    private static float ModelScale(float[] factors, int bone)
    {
        var scale = 1f;
        for (var b = bone; b >= 0; b = Parents[b])
            scale *= factors[b];
        return scale;
    }

    private static float ModelScale(float[] factors, string name) => ModelScale(factors, Array.IndexOf(Names, name));

    [Fact]
    public void BodyGrowsByTheAmount()
    {
        var factors = PuffScale.LocalFactors(Names, Parents, 0.15f);
        Assert.Equal(1.15f, ModelScale(factors, "j_kosi"), 4);
        Assert.Equal(1.15f, ModelScale(factors, "j_sebo"), 4);
    }

    [Fact]
    public void HeadGrowsOnlyALittle()
    {
        var factors = PuffScale.LocalFactors(Names, Parents, 0.15f);
        var head = ModelScale(factors, "j_kubi");
        Assert.True(head > 1.01f && head < 1.1f, $"頭 {head}");
    }

    [Theory]
    [InlineData("j_mouth")]
    [InlineData("j_ude_a_l")]
    [InlineData("j_ude_b_l")]
    [InlineData("j_tail")]
    [InlineData("j_asi_a_l")]
    [InlineData("j_asi_b_l")]
    [InlineData("n_root")]
    public void BeakWingsTailAndLegsKeepTheirSize(string bone)
    {
        var factors = PuffScale.LocalFactors(Names, Parents, 0.15f);
        Assert.Equal(1f, ModelScale(factors, bone), 4);
    }

    [Fact]
    public void ZeroAmountChangesNothing()
    {
        var factors = PuffScale.LocalFactors(Names, Parents, 0f);
        Assert.All(factors, f => Assert.Equal(1f, f, 6));
    }

    [Fact]
    public void UnnamedBonesKeepTheirSize()
    {
        string?[] names = [null, "j_sebo", null];
        int[] parents = [-1, 0, 1];
        var factors = PuffScale.LocalFactors(names, parents, 0.2f);
        Assert.Equal(1f, factors[0], 6);
        Assert.Equal(1.2f, factors[1], 6);
        Assert.Equal(1f, factors[1] * factors[2], 6);
    }
}
