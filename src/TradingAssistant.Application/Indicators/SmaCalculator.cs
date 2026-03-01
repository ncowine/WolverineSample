namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Simple Moving Average: sum of last N prices / N.
/// First N-1 values are 0 (warmup period).
/// </summary>
public class SmaCalculator : IIndicatorCalculator
{
    public static readonly SmaCalculator Instance = new();

    public decimal[] Calculate(decimal[] prices, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        var result = new decimal[prices.Length];
        if (prices.Length < period)
            return result;

        // Compute initial sum for first complete window
        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += prices[i];

        result[period - 1] = sum / period;

        // Slide window: add new price, remove oldest
        for (var i = period; i < prices.Length; i++)
        {
            sum += prices[i] - prices[i - period];
            result[i] = sum / period;
        }

        return result;
    }
}
