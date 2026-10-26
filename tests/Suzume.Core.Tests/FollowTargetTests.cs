using System.Numerics;
using Suzume.Core;

namespace Suzume.Core.Tests;

public class FollowTargetTests
{
    [Fact]
    public void SitsBehindAndBesidePlayer()
    {
        var player = new Vector3(10f, 2f, 5f);
        var target = FollowTarget.For(player, 0f);
        Assert.Equal(player.X + FollowTarget.SideOffset, target.X, 4);
        Assert.Equal(player.Y, target.Y, 4);
        Assert.Equal(player.Z - FollowTarget.BackOffset, target.Z, 4);
    }

    [Fact]
    public void RotatesWithPlayerFacing()
    {
        // +X を向いているときは、後ろが -X、横が -Z になる
        var target = FollowTarget.For(Vector3.Zero, MathF.PI / 2f);
        Assert.Equal(-FollowTarget.BackOffset, target.X, 4);
        Assert.Equal(-FollowTarget.SideOffset, target.Z, 4);
    }
}

public class GroundTrackerTests
{
    [Fact]
    public void FollowsPlayerFeetOnGround()
    {
        var ground = new GroundTracker();
        Assert.Equal(1f, ground.Update(1f, jumping: false));
        Assert.Equal(1.5f, ground.Update(1.5f, jumping: false));
    }

    [Fact]
    public void HoldsLastGroundWhileJumping()
    {
        var ground = new GroundTracker();
        ground.Update(1f, jumping: false);
        Assert.Equal(1f, ground.Update(2.2f, jumping: true));
        Assert.Equal(1f, ground.Update(2.8f, jumping: true));
        Assert.Equal(1.1f, ground.Update(1.1f, jumping: false));
    }

    [Fact]
    public void UsesPlayerHeightWhenJumpingWithoutHistory()
    {
        var ground = new GroundTracker();
        Assert.Equal(3f, ground.Update(3f, jumping: true));
    }

    [Fact]
    public void ResetForgetsHistory()
    {
        var ground = new GroundTracker();
        ground.Update(1f, jumping: false);
        ground.Reset();
        Assert.Equal(5f, ground.Update(5f, jumping: true));
    }
}
