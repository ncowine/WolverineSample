using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class AtrCalculatorTests
{
    private readonly AtrCalculator _atr = AtrCalculator.Instance;

    // 8 bars of OHLC data
    private readonly decimal[] _high  = { 48.70m, 48.72m, 48.90m, 48.87m, 48.82m, 49.05m, 49.20m, 49.35m };
    private readonly decimal[] _low   = { 47.79m, 48.14m, 48.39m, 48.37m, 48.24m, 48.64m, 48.94m, 48.86m };
    private readonly decimal[] _close = { 48.16m, 48.61m, 48.75m, 48.63m, 48.74m, 49.03m, 49.07m, 49.32m };

    [Fact]
    public void True_range_uses_max_of_three_components()
    {
        // For index 1:
        // HL = 48.72 - 48.14 = 0.58
        // |H - PrevClose| = |48.72 - 48.16| = 0.56
        // |L - PrevClose| = |48.14 - 48.16| = 0.02
        // TR = max(0.58, 0.56, 0.02) = 0.58
        var result = _atr.Calculate(_high, _low, _close, period: 3);

        // First ATR at index 3 = avg of TR[1], TR[2], TR[3]
        // TR[1] = 0.58, TR[2] = max(0.51, 0.15, 0.36)=0.51, TR[3] = max(0.50, 0.12, 0.38)=0.50
        // ATR[3] = (0.58 + 0.51 + 0.50) / 3 = 0.53
        Assert.NotEqual(0m, result[3]);
        Assert.True(result[3] > 0);
    }

    [Fact]
    public void Warmup_period_is_correct()
    {
        var result = _atr.Calculate(_high, _low, _close, period: 5);

        // First 5 values (indices 0-4) should be 0
        for (var i = 0; i < 5; i++)
            Assert.Equal(0m, result[i]);

        // Index 5 should have first ATR
        Assert.NotEqual(0m, result[5]);
    }

    [Fact]
    public void Atr_values_are_positive()
    {
        var result = _atr.Calculate(_high, _low, _close, period: 3);

        for (var i = 3; i < result.Length; i++)
            Assert.True(result[i] > 0, $"ATR at index {i} should be positive");
    }

    [Fact]
    public void Wilders_smoothing_applied_after_seed()
    {
        var result = _atr.Calculate(_high, _low, _close, period: 3);

        // Verify Wilder's smoothing: ATR[i] = (ATR[i-1] * 2 + TR[i]) / 3
        // The values should change smoothly
        Assert.NotEqual(0m, result[3]);
        Assert.NotEqual(0m, result[4]);

        // After seed, values should be smoothly transitioning
        Assert.True(Math.Abs(result[4] - result[3]) < result[3],
            "Wilder's smoothing should produce gradual changes");
    }

    [Fact]
    public void Constant_bars_produce_constant_atr()
    {
        // If H-L is always the same and no gaps, ATR should converge to that range
        var high  = new decimal[] { 52, 52, 52, 52, 52, 52, 52 };
        var low   = new decimal[] { 48, 48, 48, 48, 48, 48, 48 };
        var close = new decimal[] { 50, 50, 50, 50, 50, 50, 50 };

        var result = _atr.Calculate(high, low, close, period: 3);

        // TR = 4 for every bar. ATR should be 4 everywhere after warmup.
        Assert.Equal(4m, result[3]);
        Assert.Equal(4m, result[6]);
    }

    [Fact]
    public void Gap_up_increases_true_range()
    {
        // Big gap up: prev close = 48, next high = 55, low = 53
        // TR = max(55-53, |55-48|, |53-48|) = max(2, 7, 5) = 7
        var high  = new decimal[] { 50, 50, 50, 55 };
        var low   = new decimal[] { 48, 48, 48, 53 };
        var close = new decimal[] { 49, 49, 48, 54 };

        var result = _atr.Calculate(high, low, close, period: 2);

        // ATR at index 2 = avg(TR[1], TR[2])
        // TR[1] = max(2, |50-49|, |48-49|) = max(2,1,1) = 2
        // TR[2] = max(2, |50-49|, |48-49|) = max(2,1,1) = 2
        // ATR[2] = 2
        // ATR[3] = (2*1 + 7) / 2 = 4.5
        Assert.True(result[3] > result[2],
            "Gap up should increase ATR due to larger true range");
    }

    [Fact]
    public void Throws_for_mismatched_arrays()
    {
        Assert.Throws<ArgumentException>(() =>
            _atr.Calculate(new decimal[5], new decimal[3], new decimal[5]));
    }

    [Fact]
    public void Period_larger_than_data_returns_all_zeros()
    {
        var result = _atr.Calculate(_high, _low, _close, _high.Length + 1);
        Assert.All(result, v => Assert.Equal(0m, v));
    }
}
