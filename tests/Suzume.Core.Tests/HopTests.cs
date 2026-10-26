using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class HopTests
{
    [Fact]
    public void StartsAndEndsAtEndpoints()
    {
        var hop = new Hop(Vector3.Zero, new Vector3(0.2f, 0f, 0f), 0.08f, 0.12f);
        Assert.Equal(Vector3.Zero, hop.Evaluate(0f));
        Assert.Equal(hop.To, hop.Evaluate(0.12f));
        Assert.Equal(hop.To, hop.Evaluate(1f));
    }

    [Fact]
    public void PeaksAtHeightHalfway()
    {
        var hop = new Hop(Vector3.Zero, new Vector3(0.2f, 0.1f, 0f), 0.08f, 0.12f);
        var mid = hop.Evaluate(0.06f);
        Assert.Equal(0.1f, mid.X, 4);
        Assert.Equal(0.05f + 0.08f, mid.Y, 4);
    }
}
