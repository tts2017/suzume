using Suzume.Core;

namespace Suzume.Core.Tests;

public class GroundCalibrationTests
{
    [Fact]
    public void LearnsHowHighCharactersStandAboveCollision()
    {
        var calibration = new GroundCalibration();
        // プレイヤーは当たり判定 36.00 の上、36.09 に立っている
        for (var i = 0; i < 120; i++)
            calibration.Update(playerY: 36.09f, groundUnderPlayer: 36.00f, grounded: true);

        Assert.Equal(36.09f, calibration.Apply(36.00f), 3);
        Assert.Equal(40.09f, calibration.Apply(40.00f), 3);
    }

    [Fact]
    public void IgnoresSamplesWhileJumping()
    {
        var calibration = new GroundCalibration();
        calibration.Update(playerY: 37.5f, groundUnderPlayer: 36.00f, grounded: false);

        Assert.Equal(36.00f, calibration.Apply(36.00f), 3);
    }

    [Fact]
    public void IgnoresImplausibleDifferences()
    {
        var calibration = new GroundCalibration();
        // 足元の下に別の床がある(橋の上など)ときは測らない
        calibration.Update(playerY: 36.09f, groundUnderPlayer: 30f, grounded: true);
        calibration.Update(playerY: 36.09f, groundUnderPlayer: null, grounded: true);

        Assert.Equal(36.00f, calibration.Apply(36.00f), 3);
    }

    [Fact]
    public void ChangesSmoothlyRatherThanJumping()
    {
        var calibration = new GroundCalibration();
        calibration.Update(playerY: 36.09f, groundUnderPlayer: 36.00f, grounded: true);

        var once = calibration.Apply(36.00f) - 36.00f;
        Assert.InRange(once, 0.001f, 0.05f);
    }
}
