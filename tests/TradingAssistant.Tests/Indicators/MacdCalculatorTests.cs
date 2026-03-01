using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class MacdCalculatorTests
{
    private readonly MacdCalculator _macd = MacdCalculator.Instance;

    // 30 prices to have enough for MACD(12,26,9): need >=26 for slow EMA + 9 for signal = 34 ideal, 26 minimum
    private readonly decimal[] _prices;

    public MacdCalculatorTests()
    {
        // Generate 40 prices with an uptrend pattern
        _prices = new decimal[40];
        for (var i = 0; i < 40; i++)
            _prices[i] = 100m + i * 0.5m + (i % 3 == 0 ? 1m : -0.3m);
    }

    [Fact]
    public void Macd_line_is_fast_minus_slow_ema()
    {
        var result = _macd.Calculate(_prices, 12, 26, 9);
        var fastEma = EmaCalculator.Instance.Calculate(_prices, 12);
        var slowEma = EmaCalculator.Instance.Calculate(_prices, 26);

        // After slow EMA warmup (index 25), MACD should equal fast - slow
        for (var i = 25; i < _prices.Length; i++)
        {
            Assert.Equal(fastEma[i] - slowEma[i], result.Macd[i], 10);
        }
    }

    [Fact]
    public void Warmup_period_for_macd_line()
    {
        var result = _macd.Calculate(_prices, 12, 26, 9);

        // First 25 MACD values should be 0 (slow period - 1)
        for (var i = 0; i < 25; i++)
            Assert.Equal(0m, result.Macd[i]);

        Assert.NotEqual(0m, result.Macd[25]);
    }

    [Fact]
    public void Signal_line_warmup()
    {
        var result = _macd.Calculate(_prices, 12, 26, 9);

        // Signal starts at index 25 + 9 - 1 = 33
        var signalStart = 25 + 9 - 1; // = 33
        for (var i = 0; i < signalStart; i++)
            Assert.Equal(0m, result.Signal[i]);

        Assert.NotEqual(0m, result.Signal[signalStart]);
    }

    [Fact]
    public void Histogram_equals_macd_minus_signal()
    {
        var result = _macd.Calculate(_prices, 12, 26, 9);
        var signalStart = 25 + 9 - 1;

        for (var i = signalStart; i < _prices.Length; i++)
        {
            Assert.Equal(result.Macd[i] - result.Signal[i], result.Histogram[i], 10);
        }
    }

    [Fact]
    public void Throws_when_fast_not_less_than_slow()
    {
        Assert.Throws<ArgumentException>(() => _macd.Calculate(_prices, 26, 12, 9));
        Assert.Throws<ArgumentException>(() => _macd.Calculate(_prices, 12, 12, 9));
    }

    [Fact]
    public void Insufficient_data_returns_all_zeros()
    {
        var shortPrices = new decimal[] { 10, 11, 12 };
        var result = _macd.Calculate(shortPrices, 12, 26, 9);

        Assert.All(result.Macd, v => Assert.Equal(0m, v));
        Assert.All(result.Signal, v => Assert.Equal(0m, v));
        Assert.All(result.Histogram, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void Custom_periods_work()
    {
        // Use smaller periods so we can verify with fewer data points
        var prices = new decimal[] { 10, 11, 12, 11, 13, 12, 14, 13, 15, 14, 16, 15 };
        var result = _macd.Calculate(prices, 3, 5, 3);

        // MACD line starts at index 4 (slow period - 1)
        Assert.NotEqual(0m, result.Macd[4]);

        // Signal starts at index 4 + 3 - 1 = 6
        Assert.NotEqual(0m, result.Signal[6]);
    }

    [Fact]
    public void Uptrend_produces_positive_macd()
    {
        // Strong uptrend: fast EMA reacts faster → MACD > 0
        var uptrend = new decimal[40];
        for (var i = 0; i < 40; i++)
            uptrend[i] = 100m + i * 2m;

        var result = _macd.Calculate(uptrend, 12, 26, 9);

        // After warmup, MACD should be positive
        for (var i = 25; i < uptrend.Length; i++)
            Assert.True(result.Macd[i] > 0, $"MACD at index {i} should be positive in uptrend");
    }
}
