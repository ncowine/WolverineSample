namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Average True Range using Wilder's smoothing.
/// True Range = max(High-Low, |High-PrevClose|, |Low-PrevClose|)
/// First ATR = simple average of first N true ranges.
/// Subsequent: ATR = (prevATR * (period-1) + currentTR) / period
/// Index 0 is always 0 (no previous close). First ATR at index [period].
/// </summary>
public class AtrCalculator
{
    public static readonly AtrCalculator Instance = new();

    public decimal[] Calculate(decimal[] high, decimal[] low, decimal[] close, int period = 14)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(period, 1, nameof(period));

        if (high.Length != low.Length || high.Length != close.Length)
            throw new ArgumentException("High, low, and close arrays must have the same length.");

        var length = high.Length;
        var result = new decimal[length];

        if (length <= period)
            return result;

        // Calculate true ranges (index 0 has no previous close, so TR = High - Low)
        var trueRanges = new decimal[length];
        trueRanges[0] = high[0] - low[0];

        for (var i = 1; i < length; i++)
        {
            var hl = high[i] - low[i];
            var hpc = Math.Abs(high[i] - close[i - 1]);
            var lpc = Math.Abs(low[i] - close[i - 1]);
            trueRanges[i] = Math.Max(hl, Math.Max(hpc, lpc));
        }

        // First ATR = simple average of true ranges [1..period]
        var sum = 0m;
        for (var i = 1; i <= period; i++)
            sum += trueRanges[i];

        result[period] = sum / period;

        // Wilder's smoothing for remaining bars
        for (var i = period + 1; i < length; i++)
        {
            result[i] = (result[i - 1] * (period - 1) + trueRanges[i]) / period;
        }

        return result;
    }
}
