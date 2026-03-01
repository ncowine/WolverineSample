namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Detects crossover points between two indicator series (e.g. fast/slow moving averages).
/// </summary>
public static class CrossoverDetector
{
    /// <summary>
    /// Detects all crossover points between fast and slow series.
    /// Returns a list of crossover events with index, type (bullish/bearish), and values.
    /// Skips warmup bars where either series is 0.
    /// </summary>
    public static List<CrossoverPoint> Detect(decimal[] fast, decimal[] slow)
    {
        if (fast.Length != slow.Length)
            throw new ArgumentException("Fast and slow arrays must have the same length.");

        var crossovers = new List<CrossoverPoint>();
        var length = fast.Length;

        // Find the first bar where both series have non-zero values
        var startIndex = -1;
        for (var i = 0; i < length; i++)
        {
            if (fast[i] != 0 && slow[i] != 0)
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0 || startIndex >= length - 1)
            return crossovers;

        for (var i = startIndex + 1; i < length; i++)
        {
            // Skip bars where either series is still in warmup
            if (fast[i] == 0 || slow[i] == 0 || fast[i - 1] == 0 || slow[i - 1] == 0)
                continue;

            var prevDiff = fast[i - 1] - slow[i - 1];
            var currDiff = fast[i] - slow[i];

            // Bullish crossover: fast crosses above slow
            if (prevDiff <= 0 && currDiff > 0)
            {
                crossovers.Add(new CrossoverPoint(i, CrossoverType.Bullish, fast[i], slow[i]));
            }
            // Bearish crossover: fast crosses below slow
            else if (prevDiff >= 0 && currDiff < 0)
            {
                crossovers.Add(new CrossoverPoint(i, CrossoverType.Bearish, fast[i], slow[i]));
            }
        }

        return crossovers;
    }
}

public enum CrossoverType
{
    Bullish,
    Bearish
}

public record CrossoverPoint(int Index, CrossoverType Type, decimal FastValue, decimal SlowValue);
