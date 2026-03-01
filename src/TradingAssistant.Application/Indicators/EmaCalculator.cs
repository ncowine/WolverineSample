namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Exponential Moving Average.
/// Uses SMA of the first N values as the seed, then applies the EMA formula:
///   EMA_today = Price * k + EMA_yesterday * (1 - k)
///   where k = 2 / (period + 1)
/// First N-1 values are 0 (warmup period).
/// </summary>
public class EmaCalculator : IIndicatorCalculator
{
    public static readonly EmaCalculator Instance = new();

    public decimal[] Calculate(decimal[] prices, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        var result = new decimal[prices.Length];
        if (prices.Length < period)
            return result;

        var k = 2m / (period + 1);

        // Seed: SMA of first N prices
        var sum = 0m;
        for (var i = 0; i < period; i++)
            sum += prices[i];

        result[period - 1] = sum / period;

        // Apply EMA formula from period onward
        for (var i = period; i < prices.Length; i++)
        {
            result[i] = prices[i] * k + result[i - 1] * (1 - k);
        }

        return result;
    }
}
