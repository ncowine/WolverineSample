using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class CrossoverDetectorTests
{
    [Fact]
    public void Detects_bullish_crossover()
    {
        // Fast crosses above slow at index 3
        var fast = new decimal[] { 0, 0, 10, 12, 16, 18 };
        var slow = new decimal[] { 0, 0, 14, 14, 14, 14 };

        var crossovers = CrossoverDetector.Detect(fast, slow);

        Assert.Single(crossovers);
        Assert.Equal(4, crossovers[0].Index);
        Assert.Equal(CrossoverType.Bullish, crossovers[0].Type);
        Assert.Equal(16m, crossovers[0].FastValue);
        Assert.Equal(14m, crossovers[0].SlowValue);
    }

    [Fact]
    public void Detects_bearish_crossover()
    {
        // Fast is above slow at index 2-3, then crosses below at index 4
        var fast = new decimal[] { 0, 0, 16, 14, 10, 8 };
        var slow = new decimal[] { 0, 0, 12, 12, 12, 12 };

        var crossovers = CrossoverDetector.Detect(fast, slow);

        Assert.Single(crossovers);
        Assert.Equal(4, crossovers[0].Index);
        Assert.Equal(CrossoverType.Bearish, crossovers[0].Type);
    }

    [Fact]
    public void Detects_multiple_crossovers()
    {
        // Two crossovers: bullish at index 2, bearish at index 4
        var fast = new decimal[] { 8, 10, 14, 14, 10, 8 };
        var slow = new decimal[] { 12, 12, 12, 12, 12, 12 };

        var crossovers = CrossoverDetector.Detect(fast, slow);

        Assert.Equal(2, crossovers.Count);
        Assert.Equal(CrossoverType.Bullish, crossovers[0].Type);
        Assert.Equal(2, crossovers[0].Index);
        Assert.Equal(CrossoverType.Bearish, crossovers[1].Type);
        Assert.Equal(4, crossovers[1].Index);
    }

    [Fact]
    public void Skips_warmup_zeros()
    {
        // Zeros in first 3 positions (warmup), then crossover at index 5
        var fast = new decimal[] { 0, 0, 0, 10, 12, 16 };
        var slow = new decimal[] { 0, 0, 0, 14, 14, 14 };

        var crossovers = CrossoverDetector.Detect(fast, slow);

        Assert.Single(crossovers);
        Assert.Equal(5, crossovers[0].Index);
        Assert.Equal(CrossoverType.Bullish, crossovers[0].Type);
    }

    [Fact]
    public void No_crossover_when_fast_always_above()
    {
        var fast = new decimal[] { 20, 20, 20, 20 };
        var slow = new decimal[] { 10, 10, 10, 10 };

        var crossovers = CrossoverDetector.Detect(fast, slow);

        Assert.Empty(crossovers);
    }

    [Fact]
    public void Throws_for_mismatched_lengths()
    {
        var fast = new decimal[] { 1, 2, 3 };
        var slow = new decimal[] { 1, 2 };

        Assert.Throws<ArgumentException>(() => CrossoverDetector.Detect(fast, slow));
    }

    [Fact]
    public void Works_with_real_moving_averages()
    {
        // Simulate a golden cross scenario with real SMA data
        // Prices trend upward, so fast (short) SMA will eventually cross above slow (long) SMA
        var prices = new decimal[] { 10, 10, 10, 10, 10, 12, 14, 16, 18, 20, 22, 24 };
        var fast = SmaCalculator.Instance.Calculate(prices, 3);
        var slow = SmaCalculator.Instance.Calculate(prices, 5);

        var crossovers = CrossoverDetector.Detect(fast, slow);

        // There should be a bullish crossover as the short-term average catches up
        Assert.Contains(crossovers, c => c.Type == CrossoverType.Bullish);
    }

    [Fact]
    public void Empty_arrays_returns_empty()
    {
        var crossovers = CrossoverDetector.Detect(Array.Empty<decimal>(), Array.Empty<decimal>());
        Assert.Empty(crossovers);
    }
}
