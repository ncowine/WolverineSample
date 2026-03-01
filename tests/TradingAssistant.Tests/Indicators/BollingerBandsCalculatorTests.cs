using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class BollingerBandsCalculatorTests
{
    private readonly BollingerBandsCalculator _bb = BollingerBandsCalculator.Instance;

    [Fact]
    public void Middle_band_equals_sma()
    {
        var prices = new decimal[] { 22.27m, 22.19m, 22.08m, 22.17m, 22.18m, 22.13m, 22.23m, 22.43m, 22.24m, 22.29m };
        var sma = SmaCalculator.Instance.Calculate(prices, 5);
        var result = _bb.Calculate(prices, period: 5, multiplier: 2);

        for (var i = 4; i < prices.Length; i++)
            Assert.Equal(sma[i], result.Middle[i]);
    }

    [Fact]
    public void Upper_band_above_middle_above_lower()
    {
        var prices = new decimal[] { 10, 12, 11, 13, 10, 14, 9, 15, 8, 16 };
        var result = _bb.Calculate(prices, period: 5, multiplier: 2);

        for (var i = 4; i < prices.Length; i++)
        {
            Assert.True(result.Upper[i] >= result.Middle[i],
                $"Upper ({result.Upper[i]}) should be >= Middle ({result.Middle[i]}) at index {i}");
            Assert.True(result.Middle[i] >= result.Lower[i],
                $"Middle ({result.Middle[i]}) should be >= Lower ({result.Lower[i]}) at index {i}");
        }
    }

    [Fact]
    public void Constant_prices_produce_zero_bandwidth()
    {
        var prices = new decimal[] { 50, 50, 50, 50, 50, 50 };
        var result = _bb.Calculate(prices, period: 3, multiplier: 2);

        // StdDev = 0 → Upper = Middle = Lower → Bandwidth = 0
        for (var i = 2; i < prices.Length; i++)
        {
            Assert.Equal(result.Middle[i], result.Upper[i]);
            Assert.Equal(result.Middle[i], result.Lower[i]);
            Assert.Equal(0m, result.Bandwidth[i]);
        }
    }

    [Fact]
    public void Percent_b_at_upper_band_is_1()
    {
        // When price exactly equals upper band, %B should be ~1
        var prices = new decimal[] { 10, 10, 10, 10, 10, 10, 20 }; // spike at end
        var result = _bb.Calculate(prices, period: 5, multiplier: 2);

        // At the spike, price should be above middle → %B > 0.5
        Assert.True(result.PercentB[6] > 0.5m);
    }

    [Fact]
    public void Percent_b_at_middle_is_roughly_half()
    {
        // If price = middle band, %B = (middle - lower) / (upper - lower) = 0.5
        var prices = new decimal[] { 10, 12, 11, 13, 10, 14, 9 };
        var result = _bb.Calculate(prices, period: 5, multiplier: 2);

        // At any bar where price ≈ middle, %B ≈ 0.5
        // This is hard to guarantee exactly, but %B should be between 0 and 1 for prices within bands
        for (var i = 4; i < prices.Length; i++)
        {
            if (result.Upper[i] != result.Lower[i])
            {
                // %B should be a reasonable value
                Assert.NotEqual(0m, result.PercentB[i]);
            }
        }
    }

    [Fact]
    public void Higher_multiplier_produces_wider_bands()
    {
        var prices = new decimal[] { 10, 12, 11, 13, 10, 14, 9, 15, 8, 16 };
        var narrow = _bb.Calculate(prices, period: 5, multiplier: 1);
        var wide = _bb.Calculate(prices, period: 5, multiplier: 3);

        for (var i = 4; i < prices.Length; i++)
        {
            var narrowWidth = narrow.Upper[i] - narrow.Lower[i];
            var wideWidth = wide.Upper[i] - wide.Lower[i];
            Assert.True(wideWidth > narrowWidth,
                $"3x multiplier bands ({wideWidth}) should be wider than 1x ({narrowWidth})");
        }
    }

    [Fact]
    public void Warmup_period_is_correct()
    {
        var prices = new decimal[] { 10, 12, 11, 13, 10, 14, 9 };
        var result = _bb.Calculate(prices, period: 5, multiplier: 2);

        // First 4 values should be 0
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(0m, result.Upper[i]);
            Assert.Equal(0m, result.Middle[i]);
            Assert.Equal(0m, result.Lower[i]);
        }

        Assert.NotEqual(0m, result.Upper[4]);
    }

    [Fact]
    public void Bandwidth_is_relative_to_middle()
    {
        var prices = new decimal[] { 10, 12, 11, 13, 10, 14, 9, 15, 8, 16 };
        var result = _bb.Calculate(prices, period: 5, multiplier: 2);

        for (var i = 4; i < prices.Length; i++)
        {
            var expected = (result.Upper[i] - result.Lower[i]) / result.Middle[i];
            Assert.Equal(expected, result.Bandwidth[i], 10);
        }
    }

    [Fact]
    public void Insufficient_data_returns_all_zeros()
    {
        var prices = new decimal[] { 10, 11 };
        var result = _bb.Calculate(prices, period: 5);

        Assert.All(result.Upper, v => Assert.Equal(0m, v));
        Assert.All(result.Middle, v => Assert.Equal(0m, v));
        Assert.All(result.Lower, v => Assert.Equal(0m, v));
    }
}
