using Suzume.Core;

namespace Suzume.Core.Tests;

public class ChirpSchedulerTests
{
    private const float Dt = 1f / 60f;

    [Fact]
    public void AlarmAndFlightIgnoreCooldown()
    {
        var scheduler = new ChirpScheduler(new SparrowParams(), new SequenceRandom(0f));
        Assert.Equal(ChirpKind.Contact, scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.None));
        Assert.Equal(ChirpKind.Alarm, scheduler.Update(Dt, SparrowState.Alarm, SparrowTrigger.Alarm));
        Assert.Equal(ChirpKind.Flight, scheduler.Update(Dt, SparrowState.TakeOff, SparrowTrigger.TakeOff));
    }

    [Fact]
    public void RandomChirpsRespectMinimumInterval()
    {
        var p = new SparrowParams { ContactRatePerSecond = 50f };
        var scheduler = new ChirpScheduler(p, new SeededRandom(3));
        var times = new List<float>();

        for (var i = 0; i < 60 * 60; i++)
        {
            if (scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.None) is not null)
                times.Add(i * Dt);
        }

        Assert.True(times.Count > 10);
        Assert.All(times.Zip(times.Skip(1)), pair =>
            Assert.True(pair.Second - pair.First >= p.ChirpMinInterval - Dt));
    }

    [Fact]
    public void ZeroFrequencySilencesRandomChirps()
    {
        var scheduler = new ChirpScheduler(new SparrowParams { ChirpFrequency = 0f }, new SequenceRandom(0f));
        for (var i = 0; i < 600; i++)
            Assert.Null(scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.LongIdleStart));
    }

    [Fact]
    public void LongIdleMaySing()
    {
        var scheduler = new ChirpScheduler(new SparrowParams { SongChance = 1f }, new SequenceRandom(0f));
        Assert.Equal(ChirpKind.Song, scheduler.Update(Dt, SparrowState.Idle, SparrowTrigger.LongIdleStart));
    }

    [Fact]
    public void SilentWhileFlying()
    {
        var scheduler = new ChirpScheduler(new SparrowParams { ContactRatePerSecond = 1000f }, new SequenceRandom(0f));
        Assert.Null(scheduler.Update(Dt, SparrowState.Fly, SparrowTrigger.None));
    }
}

public class ChirpVolumeTests
{
    [Theory]
    [InlineData(0f, 0.6f)]
    [InlineData(5f, 0.6f)]
    [InlineData(17.5f, 0.3f)]
    [InlineData(30f, 0f)]
    [InlineData(100f, 0f)]
    public void AttenuatesLinearlyBetween5And30(float distance, float expected) =>
        Assert.Equal(expected, ChirpVolume.Compute(0.6f, distance), 4);

    [Fact]
    public void ClampsBaseVolume() =>
        Assert.Equal(1f, ChirpVolume.Compute(3f, 0f), 4);
}
