using Suzume.Core;

namespace Suzume.Core.Tests;

public class RandomTests
{
    [Theory]
    [InlineData(0f, 2)]
    [InlineData(0.5f, 4)]
    [InlineData(0.9999f, 6)]
    public void RangeIntCoversInclusiveBounds(float value, int expected) =>
        Assert.Equal(expected, new SequenceRandom(value).RangeInt(2, 6));

    [Fact]
    public void RangeScalesLinearly() =>
        Assert.Equal(1.5f, new SequenceRandom(0.25f).Range(1f, 3f), 5);

    [Fact]
    public void ChanceComparesAgainstProbability()
    {
        Assert.True(new SequenceRandom(0.1f).Chance(0.2f));
        Assert.False(new SequenceRandom(0.3f).Chance(0.2f));
    }

    [Fact]
    public void SeededRandomIsDeterministic()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);
        for (var i = 0; i < 10; i++)
            Assert.Equal(a.NextFloat(), b.NextFloat());
    }
}
