namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Detects divergences between price and an indicator (e.g. RSI, MACD).
/// Bearish divergence: price makes a higher high but indicator makes a lower high.
/// Bullish divergence: price makes a lower low but indicator makes a higher low.
/// Uses swing highs/lows within a lookback window.
/// </summary>
public static class DivergenceDetector
{
    /// <summary>
    /// Detects divergence points between price and an indicator series.
    /// </summary>
    /// <param name="prices">Close prices.</param>
    /// <param name="indicator">Indicator values (e.g. RSI).</param>
    /// <param name="swingStrength">Number of bars on each side to confirm a swing point (default 5).</param>
    /// <param name="maxLookback">Max bars between two swing points to compare (default 60).</param>
    public static List<DivergencePoint> Detect(decimal[] prices, decimal[] indicator,
        int swingStrength = 5, int maxLookback = 60)
    {
        if (prices.Length != indicator.Length)
            throw new ArgumentException("Price and indicator arrays must have the same length.");

        var divergences = new List<DivergencePoint>();
        var length = prices.Length;

        if (length < swingStrength * 2 + 1)
            return divergences;

        // Find swing highs and lows
        var swingHighs = FindSwingHighs(prices, swingStrength);
        var swingLows = FindSwingLows(prices, swingStrength);

        // Check for bearish divergence: higher price high + lower indicator high
        for (var i = 1; i < swingHighs.Count; i++)
        {
            var prev = swingHighs[i - 1];
            var curr = swingHighs[i];

            if (curr.Index - prev.Index > maxLookback)
                continue;

            // Skip if indicator is still in warmup (zero)
            if (indicator[prev.Index] == 0 || indicator[curr.Index] == 0)
                continue;

            if (prices[curr.Index] > prices[prev.Index] &&
                indicator[curr.Index] < indicator[prev.Index])
            {
                divergences.Add(new DivergencePoint(
                    curr.Index, DivergenceType.Bearish,
                    prices[curr.Index], indicator[curr.Index]));
            }
        }

        // Check for bullish divergence: lower price low + higher indicator low
        for (var i = 1; i < swingLows.Count; i++)
        {
            var prev = swingLows[i - 1];
            var curr = swingLows[i];

            if (curr.Index - prev.Index > maxLookback)
                continue;

            if (indicator[prev.Index] == 0 || indicator[curr.Index] == 0)
                continue;

            if (prices[curr.Index] < prices[prev.Index] &&
                indicator[curr.Index] > indicator[prev.Index])
            {
                divergences.Add(new DivergencePoint(
                    curr.Index, DivergenceType.Bullish,
                    prices[curr.Index], indicator[curr.Index]));
            }
        }

        return divergences.OrderBy(d => d.Index).ToList();
    }

    internal static List<SwingPoint> FindSwingHighs(decimal[] prices, int strength)
    {
        var swingHighs = new List<SwingPoint>();

        for (var i = strength; i < prices.Length - strength; i++)
        {
            var isSwingHigh = true;
            for (var j = 1; j <= strength; j++)
            {
                if (prices[i] <= prices[i - j] || prices[i] <= prices[i + j])
                {
                    isSwingHigh = false;
                    break;
                }
            }

            if (isSwingHigh)
                swingHighs.Add(new SwingPoint(i, prices[i]));
        }

        return swingHighs;
    }

    internal static List<SwingPoint> FindSwingLows(decimal[] prices, int strength)
    {
        var swingLows = new List<SwingPoint>();

        for (var i = strength; i < prices.Length - strength; i++)
        {
            var isSwingLow = true;
            for (var j = 1; j <= strength; j++)
            {
                if (prices[i] >= prices[i - j] || prices[i] >= prices[i + j])
                {
                    isSwingLow = false;
                    break;
                }
            }

            if (isSwingLow)
                swingLows.Add(new SwingPoint(i, prices[i]));
        }

        return swingLows;
    }
}

public enum DivergenceType
{
    Bullish,
    Bearish
}

public record DivergencePoint(int Index, DivergenceType Type, decimal Price, decimal IndicatorValue);

internal record SwingPoint(int Index, decimal Value);
