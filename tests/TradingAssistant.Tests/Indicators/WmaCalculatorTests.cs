using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class WmaCalculatorTests
{
    private readonly WmaCalculator _wma = WmaCalculator.Instance;

    private readonly decimal[] _prices = { 10m, 11m, 12m, 13m, 14m };

    [Fact]
    public void Calculates_3_period_wma()
    {
        var result = _wma.Calculate(_prices, 3);

        // Warmup
        Assert.Equal(0m, result[0]);
        Assert.Equal(0m, result[1]);

        // WMA(3) at index 2: weights 1,2,3, denom=6
        // = (10*1 + 11*2 + 12*3) / 6 = (10 + 22 + 36) / 6 = 68/6 = 11.333...
        var expected2 = (10m * 1 + 11m * 2 + 12m * 3) / 6m;
        Assert.Equal(expected2, result[2], 10);

        // WMA(3) at index 3: (11*1 + 12*2 + 13*3) / 6 = (11+24+39)/6 = 74/6 = 12.333...
        var expected3 = (11m * 1 + 12m * 2 + 13m * 3) / 6m;
        Assert.Equal(expected3, result[3], 10);

        // WMA(3) at index 4: (12*1 + 13*2 + 14*3) / 6 = (12+26+42)/6 = 80/6 = 13.333...
        var expected4 = (12m * 1 + 13m * 2 + 14m * 3) / 6m;
        Assert.Equal(expected4, result[4], 10);
    }

    [Fact]
    public void Period_1_returns_prices_unchanged()
    {
        // Weight = 1, denom = 1, so WMA = price
        var result = _wma.Calculate(_prices, 1);

        for (var i = 0; i < _prices.Length; i++)
            Assert.Equal(_prices[i], result[i]);
    }

    [Fact]
    public void Period_equal_to_length_returns_single_value()
    {
        var result = _wma.Calculate(_prices, _prices.Length);

        for (var i = 0; i < _prices.Length - 1; i++)
            Assert.Equal(0m, result[i]);

        // WMA(5): weights 1,2,3,4,5, denom=15
        // = (10*1+11*2+12*3+13*4+14*5) / 15 = (10+22+36+52+70)/15 = 190/15 = 12.666...
        var expected = (10m * 1 + 11m * 2 + 12m * 3 + 13m * 4 + 14m * 5) / 15m;
        Assert.Equal(expected, result[^1], 10);
    }

    [Fact]
    public void Wma_weights_recent_prices_more_than_sma()
    {
        // Ascending prices: WMA should be higher than SMA because recent (higher) prices have more weight
        var ascending = new decimal[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var sma = SmaCalculator.Instance.Calculate(ascending, 5);
        var wma = _wma.Calculate(ascending, 5);

        // At the last bar, WMA should be higher than SMA for ascending prices
        Assert.True(wma[9] > sma[9],
            $"WMA ({wma[9]}) should weight recent higher prices more than SMA ({sma[9]})");
    }

    [Fact]
    public void Constant_prices_match_sma()
    {
        var constant = new decimal[] { 50, 50, 50, 50, 50 };
        var sma = SmaCalculator.Instance.Calculate(constant, 3);
        var wma = _wma.Calculate(constant, 3);

        // For constant prices, WMA = SMA
        for (var i = 2; i < constant.Length; i++)
            Assert.Equal(sma[i], wma[i]);
    }

    [Fact]
    public void Empty_prices_returns_empty()
    {
        var result = _wma.Calculate(Array.Empty<decimal>(), 3);
        Assert.Empty(result);
    }

    [Fact]
    public void Throws_for_zero_period()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _wma.Calculate(_prices, 0));
    }
}
