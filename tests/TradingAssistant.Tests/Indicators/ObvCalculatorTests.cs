using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class ObvCalculatorTests
{
    private readonly ObvCalculator _obv = ObvCalculator.Instance;

    [Fact]
    public void First_value_equals_first_volume()
    {
        var close = new decimal[] { 10, 11, 12 };
        var volume = new long[] { 1000, 2000, 3000 };

        var result = _obv.Calculate(close, volume);

        Assert.Equal(1000m, result[0]);
    }

    [Fact]
    public void Adds_volume_on_up_close()
    {
        var close = new decimal[] { 10, 11, 12, 13 };
        var volume = new long[] { 1000, 2000, 3000, 4000 };

        var result = _obv.Calculate(close, volume);

        // All up closes: OBV = 1000, 3000, 6000, 10000
        Assert.Equal(1000m, result[0]);
        Assert.Equal(3000m, result[1]);
        Assert.Equal(6000m, result[2]);
        Assert.Equal(10000m, result[3]);
    }

    [Fact]
    public void Subtracts_volume_on_down_close()
    {
        var close = new decimal[] { 13, 12, 11, 10 };
        var volume = new long[] { 1000, 2000, 3000, 4000 };

        var result = _obv.Calculate(close, volume);

        // All down closes: OBV = 1000, -1000, -4000, -8000
        Assert.Equal(1000m, result[0]);
        Assert.Equal(-1000m, result[1]);
        Assert.Equal(-4000m, result[2]);
        Assert.Equal(-8000m, result[3]);
    }

    [Fact]
    public void No_change_on_flat_close()
    {
        var close = new decimal[] { 10, 10, 10, 10 };
        var volume = new long[] { 1000, 5000, 3000, 2000 };

        var result = _obv.Calculate(close, volume);

        // All flat: OBV stays at first volume
        Assert.Equal(1000m, result[0]);
        Assert.Equal(1000m, result[1]);
        Assert.Equal(1000m, result[2]);
        Assert.Equal(1000m, result[3]);
    }

    [Fact]
    public void Mixed_direction()
    {
        var close = new decimal[] { 10, 12, 11, 13, 13 };
        var volume = new long[] { 1000, 2000, 1500, 3000, 500 };

        var result = _obv.Calculate(close, volume);

        // index 0: 1000
        // index 1: up → 1000 + 2000 = 3000
        // index 2: down → 3000 - 1500 = 1500
        // index 3: up → 1500 + 3000 = 4500
        // index 4: flat → 4500
        Assert.Equal(1000m, result[0]);
        Assert.Equal(3000m, result[1]);
        Assert.Equal(1500m, result[2]);
        Assert.Equal(4500m, result[3]);
        Assert.Equal(4500m, result[4]);
    }

    [Fact]
    public void Empty_arrays_returns_empty()
    {
        var result = _obv.Calculate(Array.Empty<decimal>(), Array.Empty<long>());
        Assert.Empty(result);
    }

    [Fact]
    public void Throws_for_mismatched_lengths()
    {
        Assert.Throws<ArgumentException>(() =>
            _obv.Calculate(new decimal[] { 10, 11 }, new long[] { 1000 }));
    }
}
