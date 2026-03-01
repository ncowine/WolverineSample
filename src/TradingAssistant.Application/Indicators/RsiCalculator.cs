namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Relative Strength Index using Wilder's smoothing method.
/// RSI = 100 - (100 / (1 + RS)), where RS = AvgGain / AvgLoss.
/// Wilder's smoothing: AvgGain = (prevAvgGain × (period-1) + currentGain) / period
/// First (period) values are 0 (warmup). Value at index [period] is the first RSI.
/// </summary>
public class RsiCalculator
{
    public static readonly RsiCalculator Instance = new();

    public decimal[] Calculate(decimal[] prices, int period = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        var result = new decimal[prices.Length];
        if (prices.Length <= period)
            return result;

        // Calculate initial average gain/loss using simple average of first N changes
        var gainSum = 0m;
        var lossSum = 0m;

        for (var i = 1; i <= period; i++)
        {
            var change = prices[i] - prices[i - 1];
            if (change > 0) gainSum += change;
            else lossSum += Math.Abs(change);
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;

        result[period] = avgLoss == 0 ? 100m : 100m - 100m / (1m + avgGain / avgLoss);

        // Apply Wilder's smoothing for remaining bars
        for (var i = period + 1; i < prices.Length; i++)
        {
            var change = prices[i] - prices[i - 1];
            var gain = change > 0 ? change : 0m;
            var loss = change < 0 ? Math.Abs(change) : 0m;

            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            result[i] = avgLoss == 0 ? 100m : 100m - 100m / (1m + avgGain / avgLoss);
        }

        return result;
    }
}
