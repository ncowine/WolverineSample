using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Tests.Indicators;

public class DivergenceDetectorTests
{
    [Fact]
    public void Detects_bearish_divergence()
    {
        // Price makes higher high, RSI makes lower high → bearish
        // Build a pattern: two peaks in price where second peak is higher,
        // but indicator's second peak is lower
        var prices = BuildPriceWithTwoPeaks(
            firstPeakValue: 50m, secondPeakValue: 55m);
        var indicator = BuildPriceWithTwoPeaks(
            firstPeakValue: 80m, secondPeakValue: 70m);

        var divergences = DivergenceDetector.Detect(prices, indicator, swingStrength: 2, maxLookback: 30);

        Assert.Contains(divergences, d => d.Type == DivergenceType.Bearish);
    }

    [Fact]
    public void Detects_bullish_divergence()
    {
        // Price makes lower low, indicator makes higher low → bullish
        var prices = BuildPriceWithTwoTroughs(
            firstTroughValue: 20m, secondTroughValue: 15m);
        var indicator = BuildPriceWithTwoTroughs(
            firstTroughValue: 25m, secondTroughValue: 30m);

        var divergences = DivergenceDetector.Detect(prices, indicator, swingStrength: 2, maxLookback: 30);

        Assert.Contains(divergences, d => d.Type == DivergenceType.Bullish);
    }

    [Fact]
    public void No_divergence_when_both_agree()
    {
        // Both price and indicator make higher highs → no divergence
        var prices = BuildPriceWithTwoPeaks(50m, 55m);
        var indicator = BuildPriceWithTwoPeaks(70m, 75m);

        var divergences = DivergenceDetector.Detect(prices, indicator, swingStrength: 2, maxLookback: 30);

        Assert.DoesNotContain(divergences, d => d.Type == DivergenceType.Bearish);
    }

    [Fact]
    public void Respects_max_lookback()
    {
        // Peaks too far apart → no divergence detected
        var prices = BuildPriceWithTwoPeaks(50m, 55m, gapBetweenPeaks: 70);
        var indicator = BuildPriceWithTwoPeaks(80m, 70m, gapBetweenPeaks: 70);

        var divergences = DivergenceDetector.Detect(prices, indicator, swingStrength: 2, maxLookback: 30);

        // Should not detect because peaks are >30 bars apart
        Assert.Empty(divergences);
    }

    [Fact]
    public void Skips_warmup_zeros_in_indicator()
    {
        // Indicator has zeros at swing points → should skip
        var prices = new decimal[20];
        var indicator = new decimal[20];
        for (var i = 0; i < 20; i++)
        {
            prices[i] = 50m + (i % 5 == 2 ? 10m : 0m);
            indicator[i] = 0m; // All zeros
        }

        var divergences = DivergenceDetector.Detect(prices, indicator, swingStrength: 2);
        Assert.Empty(divergences);
    }

    [Fact]
    public void Throws_for_mismatched_lengths()
    {
        Assert.Throws<ArgumentException>(() =>
            DivergenceDetector.Detect(new decimal[10], new decimal[5]));
    }

    [Fact]
    public void Too_short_data_returns_empty()
    {
        var divergences = DivergenceDetector.Detect(new decimal[3], new decimal[3], swingStrength: 2);
        Assert.Empty(divergences);
    }

    [Fact]
    public void FindSwingHighs_identifies_local_peaks()
    {
        // Clear peak at index 5
        var prices = new decimal[] { 10, 11, 12, 13, 14, 20, 14, 13, 12, 11, 10 };
        var swingHighs = DivergenceDetector.FindSwingHighs(prices, strength: 2);

        Assert.Single(swingHighs);
        Assert.Equal(5, swingHighs[0].Index);
        Assert.Equal(20m, swingHighs[0].Value);
    }

    [Fact]
    public void FindSwingLows_identifies_local_troughs()
    {
        // Clear trough at index 5
        var prices = new decimal[] { 20, 19, 18, 17, 16, 10, 16, 17, 18, 19, 20 };
        var swingLows = DivergenceDetector.FindSwingLows(prices, strength: 2);

        Assert.Single(swingLows);
        Assert.Equal(5, swingLows[0].Index);
        Assert.Equal(10m, swingLows[0].Value);
    }

    // --- Helper methods to build test price patterns ---

    private static decimal[] BuildPriceWithTwoPeaks(
        decimal firstPeakValue, decimal secondPeakValue, int gapBetweenPeaks = 10)
    {
        var length = 5 + gapBetweenPeaks + 5 + 5; // buffer before, gap, peak2, buffer after
        var prices = new decimal[length];
        var baseline = 30m;

        // Fill with baseline
        for (var i = 0; i < length; i++)
            prices[i] = baseline;

        // First peak at index 5
        var peak1 = 5;
        prices[peak1 - 2] = baseline + 2;
        prices[peak1 - 1] = baseline + 5;
        prices[peak1] = firstPeakValue;
        prices[peak1 + 1] = baseline + 5;
        prices[peak1 + 2] = baseline + 2;

        // Second peak
        var peak2 = peak1 + gapBetweenPeaks;
        if (peak2 + 2 < length)
        {
            prices[peak2 - 2] = baseline + 2;
            prices[peak2 - 1] = baseline + 5;
            prices[peak2] = secondPeakValue;
            prices[peak2 + 1] = baseline + 5;
            prices[peak2 + 2] = baseline + 2;
        }

        return prices;
    }

    private static decimal[] BuildPriceWithTwoTroughs(
        decimal firstTroughValue, decimal secondTroughValue, int gapBetweenPeaks = 10)
    {
        var length = 5 + gapBetweenPeaks + 5 + 5;
        var prices = new decimal[length];
        var baseline = 50m;

        for (var i = 0; i < length; i++)
            prices[i] = baseline;

        var trough1 = 5;
        prices[trough1 - 2] = baseline - 2;
        prices[trough1 - 1] = baseline - 5;
        prices[trough1] = firstTroughValue;
        prices[trough1 + 1] = baseline - 5;
        prices[trough1 + 2] = baseline - 2;

        var trough2 = trough1 + gapBetweenPeaks;
        if (trough2 + 2 < length)
        {
            prices[trough2 - 2] = baseline - 2;
            prices[trough2 - 1] = baseline - 5;
            prices[trough2] = secondTroughValue;
            prices[trough2 + 1] = baseline - 5;
            prices[trough2 + 2] = baseline - 2;
        }

        return prices;
    }
}
