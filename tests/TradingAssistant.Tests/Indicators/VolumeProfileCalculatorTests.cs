using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class VolumeProfileCalculatorTests
{
    private readonly VolumeProfileCalculator _vp = VolumeProfileCalculator.Instance;

    [Fact]
    public void Volume_ma_equals_sma_of_volume()
    {
        var volume = new long[] { 1000, 2000, 3000, 4000, 5000 };
        var result = _vp.Calculate(volume, period: 3);

        // MA at index 2 = (1000+2000+3000)/3 = 2000
        Assert.Equal(2000m, result.VolumeMa[2]);

        // MA at index 3 = (2000+3000+4000)/3 = 3000
        Assert.Equal(3000m, result.VolumeMa[3]);

        // MA at index 4 = (3000+4000+5000)/3 = 4000
        Assert.Equal(4000m, result.VolumeMa[4]);
    }

    [Fact]
    public void Relative_volume_above_1_means_above_average()
    {
        // Last bar has volume much higher than average
        var volume = new long[] { 1000, 1000, 1000, 1000, 5000 };
        var result = _vp.Calculate(volume, period: 3);

        // MA at index 4 = (1000+1000+5000)/3 ≈ 2333
        // RelVol = 5000 / 2333 ≈ 2.14
        Assert.True(result.RelativeVolume[4] > 1.0m);
    }

    [Fact]
    public void Relative_volume_below_1_means_below_average()
    {
        // Last bar has volume much lower than average
        var volume = new long[] { 5000, 5000, 5000, 5000, 1000 };
        var result = _vp.Calculate(volume, period: 3);

        // MA at index 4 = (5000+5000+1000)/3 ≈ 3666
        // RelVol = 1000 / 3666 ≈ 0.27
        Assert.True(result.RelativeVolume[4] < 1.0m);
    }

    [Fact]
    public void Constant_volume_gives_relative_1()
    {
        var volume = new long[] { 3000, 3000, 3000, 3000, 3000 };
        var result = _vp.Calculate(volume, period: 3);

        for (var i = 2; i < volume.Length; i++)
            Assert.Equal(1m, result.RelativeVolume[i]);
    }

    [Fact]
    public void Warmup_period_is_correct()
    {
        var volume = new long[] { 1000, 2000, 3000, 4000, 5000 };
        var result = _vp.Calculate(volume, period: 3);

        Assert.Equal(0m, result.VolumeMa[0]);
        Assert.Equal(0m, result.VolumeMa[1]);
        Assert.NotEqual(0m, result.VolumeMa[2]);
    }

    [Fact]
    public void IsAboveAverage_with_default_threshold()
    {
        var volume = new long[] { 1000, 1000, 1000, 1000, 2000 };
        var result = _vp.Calculate(volume, period: 3);

        // At index 4: MA = (1000+1000+2000)/3 ≈ 1333, RelVol = 2000/1333 ≈ 1.5 → above 1.2 threshold
        Assert.True(VolumeProfileCalculator.IsAboveAverage(result, 4));
    }

    [Fact]
    public void IsAboveAverage_returns_false_for_low_volume()
    {
        var volume = new long[] { 5000, 5000, 5000, 5000, 5000 };
        var result = _vp.Calculate(volume, period: 3);

        // RelVol = 1.0, below 1.2 threshold
        Assert.False(VolumeProfileCalculator.IsAboveAverage(result, 4));
    }

    [Fact]
    public void IsAboveAverage_returns_false_for_invalid_index()
    {
        var volume = new long[] { 1000, 2000, 3000 };
        var result = _vp.Calculate(volume, period: 3);

        Assert.False(VolumeProfileCalculator.IsAboveAverage(result, -1));
        Assert.False(VolumeProfileCalculator.IsAboveAverage(result, 100));
    }

    [Fact]
    public void Insufficient_data_returns_all_zeros()
    {
        var volume = new long[] { 1000, 2000 };
        var result = _vp.Calculate(volume, period: 5);

        Assert.All(result.VolumeMa, v => Assert.Equal(0m, v));
        Assert.All(result.RelativeVolume, v => Assert.Equal(0m, v));
    }
}
