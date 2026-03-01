namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Weighted Moving Average: recent prices are weighted more heavily.
/// Weight for position i within the window = (i + 1).
/// WMA = sum(price_i * weight_i) / sum(weights)
/// Denominator = period * (period + 1) / 2
/// First N-1 values are 0 (warmup period).
/// </summary>
public class WmaCalculator : IIndicatorCalculator
{
    public static readonly WmaCalculator Instance = new();

    public decimal[] Calculate(decimal[] prices, int period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        var result = new decimal[prices.Length];
        if (prices.Length < period)
            return result;

        var denominator = (decimal)(period * (period + 1)) / 2;

        for (var i = period - 1; i < prices.Length; i++)
        {
            var weightedSum = 0m;
            for (var j = 0; j < period; j++)
            {
                // j=0 is oldest in window (weight 1), j=period-1 is newest (weight period)
                weightedSum += prices[i - period + 1 + j] * (j + 1);
            }

            result[i] = weightedSum / denominator;
        }

        return result;
    }
}
