using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class EmaCalculatorTests
{
    private readonly EmaCalculator _ema = EmaCalculator.Instance;

    // 10-day price data for EMA(5) calculation
    // k = 2 / (5 + 1) = 0.333...
    private readonly decimal[] _prices = { 22.27m, 22.19m, 22.08m, 22.17m, 22.18m, 22.13m, 22.23m, 22.43m, 22.24m, 22.29m };

    [Fact]
    public void Calculates_5_period_ema()
    {
        var result = _ema.Calculate(_prices, 5);
        var k = 2m / 6m;

        // Warmup: first 4 values should be 0
        Assert.Equal(0m, result[0]);
        Assert.Equal(0m, result[3]);

        // Seed (index 4) = SMA(5) = (22.27+22.19+22.08+22.17+22.18)/5 = 22.178
        var sma5 = 22.178m;
        Assert.Equal(sma5, result[4]);

        // Index 5: EMA = 22.13 * k + 22.178 * (1 - k)
        var ema5 = 22.13m * k + sma5 * (1 - k);
        Assert.Equal(ema5, result[5], 10);

        // Index 6: EMA = 22.23 * k + ema5 * (1 - k)
        var ema6 = 22.23m * k + ema5 * (1 - k);
        Assert.Equal(ema6, result[6], 10);
    }

    [Fact]
    public void Seed_value_matches_sma()
    {
        // For EMA(3), seed at index 2 = SMA(3) = (22.27+22.19+22.08)/3
        var result = _ema.Calculate(_prices, 3);
        var expectedSma = (22.27m + 22.19m + 22.08m) / 3m;
        Assert.Equal(expectedSma, result[2], 10);
    }

    [Fact]
    public void Period_1_returns_prices_unchanged()
    {
        // k = 2/2 = 1, so EMA = Price * 1 + prev * 0 = Price
        var result = _ema.Calculate(_prices, 1);

        for (var i = 0; i < _prices.Length; i++)
            Assert.Equal(_prices[i], result[i]);
    }

    [Fact]
    public void Warmup_period_is_correct()
    {
        var result = _ema.Calculate(_prices, 7);

        // First 6 values (indices 0-5) should be 0
        for (var i = 0; i < 6; i++)
            Assert.Equal(0m, result[i]);

        // Index 6 should be the SMA seed (non-zero)
        Assert.NotEqual(0m, result[6]);
    }

    [Fact]
    public void Period_larger_than_data_returns_all_zeros()
    {
        var result = _ema.Calculate(_prices, _prices.Length + 5);
        Assert.All(result, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void Empty_prices_returns_empty()
    {
        var result = _ema.Calculate(Array.Empty<decimal>(), 3);
        Assert.Empty(result);
    }

    [Fact]
    public void Ema_reacts_faster_than_sma_to_price_spike()
    {
        // After a sudden price spike, EMA should be closer to the spike price than SMA
        var prices = new decimal[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 20 };
        var smaResult = SmaCalculator.Instance.Calculate(prices, 5);
        var emaResult = _ema.Calculate(prices, 5);

        // At index 9 (spike bar), EMA should react more (be higher) than SMA
        Assert.True(emaResult[9] > smaResult[9],
            $"EMA ({emaResult[9]}) should react faster than SMA ({smaResult[9]}) to spike");
    }
}
