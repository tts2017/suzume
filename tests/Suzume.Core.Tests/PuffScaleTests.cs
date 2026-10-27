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

    // 翼は体と同じだけ大きくする (元の大きさのままだと、たたんだ翼がふくらんだ体に埋まる)
    [Theory]
    [InlineData("j_ude_a_l")]
    [InlineData("j_ude_b_l")]
    public void WingsGrowWithTheBody(string bone)
    {
        var factors = PuffScale.LocalFactors(Names, Parents, 0.15f);
        Assert.Equal(1.15f, ModelScale(factors, bone), 4);
    }

    [Theory]
    [InlineData("j_mouth")]
    [InlineData("j_tail")]
    [InlineData("j_asi_a_l")]
    [InlineData("j_asi_b_l")]
    [InlineData("n_root")]
    public void BeakTailAndLegsKeepTheirSize(string bone)
    {
        var factors = PuffScale.LocalFactors(Names, Parents, 0.15f);
        Assert.Equal(1f, ModelScale(factors, bone), 4);
    }

    // ゲームの骨格 (Havok) は、親の大きさを子の位置に掛けない: 子のモデル位置 = 親の位置 + 子の局所の位置 (回転なしで考える)。
    // ふくらみで翼や頭が体についてくるよう、子の局所の位置を親のふくらみの分だけ伸ばす
    private static readonly System.Numerics.Vector3[] Offsets =
    [
        new(0f, 0f, 0f),          // n_root
        new(0f, 0.06f, 0f),       // j_kosi
        new(0f, 0f, 0f),          // j_sebo (腰と同じ所)
        new(0f, 0.04f, 0.015f),   // j_kubi
        new(0f, 0.005f, 0.03f),   // j_mouth
        new(0.03f, 0.037f, 0.02f), // j_ude_a_l
        new(0.02f, -0.03f, -0.03f), // j_ude_b_l
        new(0f, -0.02f, -0.055f), // j_tail
        new(0.02f, -0.018f, -0.012f), // j_asi_a_l
        new(0f, -0.02f, 0f),      // j_asi_b_l
    ];

    private static System.Numerics.Vector3 HavokModelPosition(float[] translationFactors, int bone)
    {
        var position = System.Numerics.Vector3.Zero;
        for (var b = bone; b >= 0; b = Parents[b])
            position += Offsets[b] * translationFactors[b];
        return position;
    }

    private static System.Numerics.Vector3 Pos(float[] f, string name) => HavokModelPosition(f, Array.IndexOf(Names, name));

    [Theory]
    [InlineData("j_ude_a_l")]
    [InlineData("j_ude_b_l")]
    [InlineData("j_kubi")]
    [InlineData("j_tail")]
    public void ChildrenOfTheBodyMoveOutWithIt(string bone)
    {
        var body = Pos(PuffScale.TranslationFactors(Names, Parents, 0f), "j_sebo");
        var before = Pos(PuffScale.TranslationFactors(Names, Parents, 0f), bone) - body;
        var after = Pos(PuffScale.TranslationFactors(Names, Parents, 0.3f), bone) - body;

        // 体と一緒に大きくなる骨 (翼の先) は体の大きさで、そうでない骨の付け根は体の大きさで離れる
        Assert.Equal(before.X * 1.3f, after.X, 4);
        Assert.Equal(before.Y * 1.3f, after.Y, 4);
        Assert.Equal(before.Z * 1.3f, after.Z, 4);
    }

    // 脚の付け根は動かさない: 足が地面にめり込まない (付け根は ふくらんだ体に隠れて、脚が短く見える)
    [Theory]
    [InlineData("j_asi_a_l")]
    [InlineData("j_asi_b_l")]
    public void FeetStayWhereTheyWere(string bone)
    {
        var before = Pos(PuffScale.TranslationFactors(Names, Parents, 0f), bone);
        var after = Pos(PuffScale.TranslationFactors(Names, Parents, 0.3f), bone);
        Assert.Equal(before.Y, after.Y, 4);
    }

    [Fact]
    public void BeakStaysOnThePuffedHead()
    {
        var factors = PuffScale.TranslationFactors(Names, Parents, 0.3f);
        var head = Pos(factors, "j_kubi");
        var beak = Pos(factors, "j_mouth") - head;
        var headScale = PuffScale.Target("j_kubi", 0.3f);
        Assert.Equal(Offsets[4].Z * headScale, beak.Z, 4);
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
