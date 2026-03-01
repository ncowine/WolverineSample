namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Stochastic Oscillator.
/// %K (fast) = (Close - LowestLow(N)) / (HighestHigh(N) - LowestLow(N)) × 100
/// %D (slow) = SMA(%K, smoothPeriod)
/// Default: kPeriod=14, dPeriod=3.
/// Requires high[], low[], close[] arrays of equal length.
/// </summary>
public class StochasticCalculator
{
    public static readonly StochasticCalculator Instance = new();

    public StochasticResult Calculate(decimal[] high, decimal[] low, decimal[] close,
        int kPeriod = 14, int dPeriod = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(kPeriod, 1, nameof(kPeriod));
        ArgumentOutOfRangeException.ThrowIfLessThan(dPeriod, 1, nameof(dPeriod));

        if (high.Length != low.Length || high.Length != close.Length)
            throw new ArgumentException("High, low, and close arrays must have the same length.");

        var length = high.Length;
        var k = new decimal[length];
        var d = new decimal[length];

        if (length < kPeriod)
            return new StochasticResult(k, d);

        // Calculate %K
        for (var i = kPeriod - 1; i < length; i++)
        {
            var highestHigh = decimal.MinValue;
            var lowestLow = decimal.MaxValue;

            for (var j = i - kPeriod + 1; j <= i; j++)
            {
                if (high[j] > highestHigh) highestHigh = high[j];
                if (low[j] < lowestLow) lowestLow = low[j];
            }

            var range = highestHigh - lowestLow;
            k[i] = range == 0 ? 50m : (close[i] - lowestLow) / range * 100m;
        }

        // Calculate %D = SMA of %K over dPeriod
        // %D starts at index (kPeriod - 1 + dPeriod - 1)
        var dStartIndex = kPeriod - 1 + dPeriod - 1;
        if (dStartIndex < length)
        {
            // Initial sum for first D value
            var sum = 0m;
            for (var i = kPeriod - 1; i < kPeriod - 1 + dPeriod; i++)
                sum += k[i];

            d[dStartIndex] = sum / dPeriod;

            // Sliding window for remaining D values
            for (var i = dStartIndex + 1; i < length; i++)
            {
                sum += k[i] - k[i - dPeriod];
                d[i] = sum / dPeriod;
            }
        }

        return new StochasticResult(k, d);
    }
}

public record StochasticResult(decimal[] K, decimal[] D);
