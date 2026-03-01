using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class SmaCalculatorTests
{
    private readonly SmaCalculator _sma = SmaCalculator.Instance;

    // Known data: daily closes for 10 days
    private readonly decimal[] _prices = { 22.27m, 22.19m, 22.08m, 22.17m, 22.18m, 22.13m, 22.23m, 22.43m, 22.24m, 22.29m };

    [Fact]
    public void Calculates_5_period_sma()
    {
        var result = _sma.Calculate(_prices, 5);

        // Warmup: first 4 values should be 0
        Assert.Equal(0m, result[0]);
        Assert.Equal(0m, result[1]);
        Assert.Equal(0m, result[2]);
        Assert.Equal(0m, result[3]);

        // SMA(5) at index 4 = (22.27 + 22.19 + 22.08 + 22.17 + 22.18) / 5 = 110.89 / 5 = 22.178
        Assert.Equal(22.178m, result[4]);

        // SMA(5) at index 5 = (22.19 + 22.08 + 22.17 + 22.18 + 22.13) / 5 = 110.75 / 5 = 22.15
        Assert.Equal(22.150m, result[5]);

        // SMA(5) at index 9 = (22.13 + 22.23 + 22.43 + 22.24 + 22.29) / 5 = 111.32 / 5 = 22.264
        Assert.Equal(22.264m, result[9]);
    }

    [Fact]
    public void Period_1_returns_prices_unchanged()
    {
        var result = _sma.Calculate(_prices, 1);

        for (var i = 0; i < _prices.Length; i++)
            Assert.Equal(_prices[i], result[i]);
    }

    [Fact]
    public void Period_equal_to_length_returns_single_value()
    {
        var result = _sma.Calculate(_prices, _prices.Length);

        for (var i = 0; i < _prices.Length - 1; i++)
            Assert.Equal(0m, result[i]);

        // Last value = average of all prices
        Assert.Equal(_prices.Average(), result[^1]);
    }

    [Fact]
    public void Period_larger_than_data_returns_all_zeros()
    {
        var result = _sma.Calculate(_prices, _prices.Length + 1);

        Assert.All(result, v => Assert.Equal(0m, v));
    }

    [Fact]
    public void Empty_prices_returns_empty()
    {
        var result = _sma.Calculate(Array.Empty<decimal>(), 5);
        Assert.Empty(result);
    }

    [Fact]
    public void Throws_for_zero_period()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sma.Calculate(_prices, 0));
    }

    [Fact]
    public void Constant_prices_return_same_value()
    {
        var constant = new decimal[] { 50, 50, 50, 50, 50 };
        var result = _sma.Calculate(constant, 3);

        Assert.Equal(50m, result[2]);
        Assert.Equal(50m, result[3]);
        Assert.Equal(50m, result[4]);
    }
}
