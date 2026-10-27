using Suzume.Core;

namespace Suzume.Core.Tests;

internal static class GroundAssert
{
    /// <summary>
    /// 地面 (高さ ground) に立っていることを確かめる。向きを変えるときにその場で小さく跳ねるので、
    /// その分 (TurnHopHeight) だけは上がっていてよい。
    /// </summary>
    public static void OnGround(float ground, SparrowPose pose) =>
        Assert.InRange(pose.Position.Y - ground, -0.005f, new SparrowParams().TurnHopHeight + 0.005f);
}
