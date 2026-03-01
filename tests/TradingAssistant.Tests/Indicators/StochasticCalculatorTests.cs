using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class StochasticCalculatorTests
{
    private readonly StochasticCalculator _stoch = StochasticCalculator.Instance;

    [Fact]
    public void Calculates_percent_k_correctly()
    {
        // Simple 5-bar example
        var high  = new decimal[] { 12, 13, 14, 13, 15, 14, 16 };
        var low   = new decimal[] { 10, 11, 12, 11, 13, 12, 14 };
        var close = new decimal[] { 11, 12, 13, 12, 14, 13, 15 };

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 3);

        // At index 4 (first K): lookback 0-4
        // HighestHigh = max(12,13,14,13,15) = 15
        // LowestLow = min(10,11,12,11,13) = 10
        // %K = (14 - 10) / (15 - 10) * 100 = 4/5 * 100 = 80
        Assert.Equal(80m, result.K[4]);
    }

    [Fact]
    public void K_warmup_period()
    {
        var high  = new decimal[] { 12, 13, 14, 13, 15, 14, 16 };
        var low   = new decimal[] { 10, 11, 12, 11, 13, 12, 14 };
        var close = new decimal[] { 11, 12, 13, 12, 14, 13, 15 };

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 3);

        // First 4 K values should be 0
        for (var i = 0; i < 4; i++)
            Assert.Equal(0m, result.K[i]);

        Assert.NotEqual(0m, result.K[4]);
    }

    [Fact]
    public void D_is_sma_of_k()
    {
        var high  = new decimal[] { 12, 13, 14, 13, 15, 14, 16, 15, 17 };
        var low   = new decimal[] { 10, 11, 12, 11, 13, 12, 14, 13, 15 };
        var close = new decimal[] { 11, 12, 13, 12, 14, 13, 15, 14, 16 };

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 3);

        // D starts at index 4 + 3 - 1 = 6
        // D[6] = SMA(K[4], K[5], K[6]) / 3
        var expectedD6 = (result.K[4] + result.K[5] + result.K[6]) / 3;
        Assert.Equal(expectedD6, result.D[6], 10);
    }

    [Fact]
    public void D_warmup_period()
    {
        var high  = new decimal[] { 12, 13, 14, 13, 15, 14, 16, 15, 17 };
        var low   = new decimal[] { 10, 11, 12, 11, 13, 12, 14, 13, 15 };
        var close = new decimal[] { 11, 12, 13, 12, 14, 13, 15, 14, 16 };

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 3);

        // D starts at index 6 (kPeriod-1 + dPeriod-1 = 4+2 = 6)
        for (var i = 0; i < 6; i++)
            Assert.Equal(0m, result.D[i]);

        Assert.NotEqual(0m, result.D[6]);
    }

    [Fact]
    public void Close_at_high_returns_k_100()
    {
        // If close equals the highest high over the period, K = 100
        var high  = new decimal[] { 10, 10, 10, 10, 20 };
        var low   = new decimal[] { 5, 5, 5, 5, 5 };
        var close = new decimal[] { 8, 8, 8, 8, 20 }; // close = highest high

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 1);

        Assert.Equal(100m, result.K[4]);
    }

    [Fact]
    public void Close_at_low_returns_k_0()
    {
        // If close equals the lowest low, K = 0
        var high  = new decimal[] { 20, 20, 20, 20, 20 };
        var low   = new decimal[] { 5, 5, 5, 5, 5 };
        var close = new decimal[] { 10, 10, 10, 10, 5 }; // close = lowest low

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 1);

        Assert.Equal(0m, result.K[4]);
    }

    [Fact]
    public void Flat_range_returns_k_50()
    {
        // If all highs and lows are the same, range=0 → K defaults to 50
        var high  = new decimal[] { 10, 10, 10, 10, 10 };
        var low   = new decimal[] { 10, 10, 10, 10, 10 };
        var close = new decimal[] { 10, 10, 10, 10, 10 };

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 1);

        Assert.Equal(50m, result.K[4]);
    }

    [Fact]
    public void Throws_for_mismatched_arrays()
    {
        Assert.Throws<ArgumentException>(() =>
            _stoch.Calculate(new decimal[] { 1, 2 }, new decimal[] { 1 }, new decimal[] { 1, 2 }));
    }

    [Fact]
    public void K_values_bounded_0_to_100()
    {
        var high  = new decimal[] { 20, 22, 18, 25, 15, 30, 12, 28, 20, 35 };
        var low   = new decimal[] { 8, 10, 6, 13, 3, 18, 1, 16, 8, 23 };
        var close = new decimal[] { 15, 18, 10, 20, 8, 25, 5, 22, 15, 30 };

        var result = _stoch.Calculate(high, low, close, kPeriod: 5, dPeriod: 3);

        for (var i = 4; i < result.K.Length; i++)
            Assert.InRange(result.K[i], 0m, 100m);
    }
}
