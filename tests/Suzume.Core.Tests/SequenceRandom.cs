using Suzume.Core;

namespace Suzume.Core.Tests;

/// <summary>決めた値を順に繰り返し返す乱数(テスト用)。</summary>
internal sealed class SequenceRandom(params float[] values) : IRandom
{
    private int index;

    public float NextFloat() => values[index++ % values.Length];
}
