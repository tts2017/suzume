using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class ChirpStereoTests
{
    private static readonly Vector3 Listener = new(10f, 0f, 20f);
    // 北 (-Z) を向いていると、東 (+X) が右
    private static readonly Vector3 North = new(0f, 0f, -1f);

    [Fact]
    public void AheadSoundsTheSameInBothEars()
    {
        var p = ChirpStereo.Place(Listener, North, Listener + new Vector3(0f, 0f, -5f));
        Assert.Equal(1f, p.LeftGain, 4);
        Assert.Equal(1f, p.RightGain, 4);
        Assert.Equal(0, p.LeftDelaySamples);
        Assert.Equal(0, p.RightDelaySamples);
    }

    [Fact]
    public void RightSideIsLouderAndEarlierInTheRightEar()
    {
        var p = ChirpStereo.Place(Listener, North, Listener + new Vector3(5f, 0f, 0f));
        Assert.Equal(1f, p.RightGain, 4);
        Assert.Equal(1f - ChirpStereo.MaxPan, p.LeftGain, 4);
        Assert.Equal(0, p.RightDelaySamples);
        Assert.Equal(ChirpStereo.MaxDelaySamples, p.LeftDelaySamples);
    }

    [Fact]
    public void LeftSideMirrorsTheRightSide()
    {
        var p = ChirpStereo.Place(Listener, North, Listener + new Vector3(-5f, 0f, 0f));
        Assert.Equal(1f, p.LeftGain, 4);
        Assert.Equal(1f - ChirpStereo.MaxPan, p.RightGain, 4);
        Assert.Equal(0, p.LeftDelaySamples);
        Assert.Equal(ChirpStereo.MaxDelaySamples, p.RightDelaySamples);
    }

    [Fact]
    public void DiagonalIsBetweenCentreAndSide()
    {
        var p = ChirpStereo.Place(Listener, North, Listener + new Vector3(5f, 0f, -5f));
        Assert.Equal(1f, p.RightGain, 4);
        Assert.InRange(p.LeftGain, 1f - ChirpStereo.MaxPan + 0.05f, 0.95f);
        Assert.InRange(p.LeftDelaySamples, 1, ChirpStereo.MaxDelaySamples - 1);
    }

    [Fact]
    public void FollowsTheListenerDirection()
    {
        // 東を向くと、南 (+Z) が右
        var p = ChirpStereo.Place(Listener, new Vector3(1f, 0f, 0f), Listener + new Vector3(0f, 0f, 5f));
        Assert.Equal(1f, p.RightGain, 4);
        Assert.True(p.LeftGain < 1f);
    }

    [Fact]
    public void HeightIsIgnored()
    {
        var level = ChirpStereo.Place(Listener, new Vector3(0f, -0.5f, -1f), Listener + new Vector3(5f, 0f, 0f));
        var above = ChirpStereo.Place(Listener, North, Listener + new Vector3(5f, 3f, 0f));
        Assert.Equal(level, above);
    }

    [Fact]
    public void RightOnTopOfTheListenerIsCentred()
    {
        var p = ChirpStereo.Place(Listener, North, Listener + new Vector3(0.1f, 1f, 0f));
        Assert.Equal(1f, p.LeftGain, 4);
        Assert.Equal(1f, p.RightGain, 4);
        Assert.Equal(0, p.LeftDelaySamples);
    }
}
