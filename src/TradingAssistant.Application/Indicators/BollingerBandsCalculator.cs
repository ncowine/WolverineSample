namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Bollinger Bands: Middle = SMA(period), Upper = Middle + multiplier*StdDev, Lower = Middle - multiplier*StdDev.
/// Also computes Bandwidth = (Upper - Lower) / Middle and %B = (Price - Lower) / (Upper - Lower).
/// Default: 20-period SMA, 2 standard deviations.
/// Warmup: first (period-1) values are 0.
/// </summary>
public class BollingerBandsCalculator
{
    public static readonly BollingerBandsCalculator Instance = new();

    public BollingerResult Calculate(decimal[] prices, int period = 20, decimal multiplier = 2m)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        var length = prices.Length;
        var upper = new decimal[length];
        var middle = new decimal[length];
        var lower = new decimal[length];
        var bandwidth = new decimal[length];
        var percentB = new decimal[length];

        if (length < period)
            return new BollingerResult(upper, middle, lower, bandwidth, percentB);

        // Compute SMA for middle band
        var sma = SmaCalculator.Instance.Calculate(prices, period);

        for (var i = period - 1; i < length; i++)
        {
            middle[i] = sma[i];

            // Calculate standard deviation over the window
            var sum = 0m;
            for (var j = i - period + 1; j <= i; j++)
            {
                var diff = prices[j] - middle[i];
                sum += diff * diff;
            }

            var stdDev = (decimal)Math.Sqrt((double)(sum / period));

            upper[i] = middle[i] + multiplier * stdDev;
            lower[i] = middle[i] - multiplier * stdDev;

            // Bandwidth = (Upper - Lower) / Middle
            bandwidth[i] = middle[i] == 0 ? 0 : (upper[i] - lower[i]) / middle[i];

            // %B = (Price - Lower) / (Upper - Lower)
            var bandRange = upper[i] - lower[i];
            percentB[i] = bandRange == 0 ? 0.5m : (prices[i] - lower[i]) / bandRange;
        }

        return new BollingerResult(upper, middle, lower, bandwidth, percentB);
    }
}

public record BollingerResult(
    decimal[] Upper,
    decimal[] Middle,
    decimal[] Lower,
    decimal[] Bandwidth,
    decimal[] PercentB);
