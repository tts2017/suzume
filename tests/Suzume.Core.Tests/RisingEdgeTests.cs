using Suzume.Core;

namespace Suzume.Core.Tests;

public class RisingEdgeTests
{
    [Fact]
    public void FiresOnlyWhenSignalTurnsOn()
    {
        var edge = new RisingEdge();
        Assert.False(edge.Update(false));
        Assert.True(edge.Update(true));
        Assert.False(edge.Update(true));
        Assert.False(edge.Update(false));
        Assert.True(edge.Update(true));
    }

    [Fact]
    public void ResetForgetsPreviousSignal()
    {
        var edge = new RisingEdge();
        edge.Update(true);
        edge.Reset();
        Assert.True(edge.Update(true));
    }
}
