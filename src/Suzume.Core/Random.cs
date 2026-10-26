namespace Suzume.Core;

/// <summary>0 以上 1 未満の乱数。テストで差し替えられるようにインターフェースにしている。</summary>
public interface IRandom
{
    float NextFloat();
}

public sealed class SeededRandom(int seed) : IRandom
{
    private readonly Random random = new(seed);

    public float NextFloat() => random.NextSingle();
}

public static class RandomExtensions
{
    public static float Range(this IRandom random, float min, float max) =>
        min + (max - min) * random.NextFloat();

    /// <summary>min 以上 maxInclusive 以下の整数。</summary>
    public static int RangeInt(this IRandom random, int min, int maxInclusive)
    {
        var offset = (int)MathF.Floor(random.NextFloat() * (maxInclusive - min + 1));
        return Math.Min(maxInclusive, min + offset);
    }

    public static bool Chance(this IRandom random, float probability) =>
        random.NextFloat() < probability;
}
