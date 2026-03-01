namespace TradingAssistant.Application.Indicators;

/// <summary>
/// On-Balance Volume: cumulative volume where volume is added on up-close days
/// and subtracted on down-close days. Flat close = no change.
/// OBV[0] = volume[0]. No warmup period.
/// </summary>
public class ObvCalculator
{
    public static readonly ObvCalculator Instance = new();

    public decimal[] Calculate(decimal[] close, long[] volume)
    {
        if (close.Length != volume.Length)
            throw new ArgumentException("Close and volume arrays must have the same length.");

        var length = close.Length;
        var result = new decimal[length];

        if (length == 0)
            return result;

        result[0] = volume[0];

        for (var i = 1; i < length; i++)
        {
            if (close[i] > close[i - 1])
                result[i] = result[i - 1] + volume[i];
            else if (close[i] < close[i - 1])
                result[i] = result[i - 1] - volume[i];
            else
                result[i] = result[i - 1]; // unchanged
        }

        return result;
    }
}
