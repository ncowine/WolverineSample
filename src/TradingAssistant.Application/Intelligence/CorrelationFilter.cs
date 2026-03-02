namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Decision from the correlation filter.
/// </summary>
public enum CorrelationAction
{
    Pass,
    Reduce,
    Block
}

/// <summary>
/// Result of a correlation check against open portfolio positions.
/// </summary>
public record CorrelationCheckResult(
    CorrelationAction Action,
    decimal AvgCorrelation,
    decimal MaxCorrelation,
    decimal SizeMultiplier,
    int PositionsChecked,
    string Detail);

/// <summary>
/// Pure static filter that checks whether a candidate position is too correlated
/// with existing portfolio holdings.
///
/// Rules:
/// - Avg pairwise correlation > BlockThreshold (0.7) → Block entry
/// - Avg pairwise correlation in [ReduceThreshold, BlockThreshold] → Reduce size proportionally
/// - Avg pairwise correlation &lt; ReduceThreshold (0.5) → Pass (full size)
/// - No open positions → always Pass
///
/// Correlation data uses "SYMBOL_A|SYMBOL_B" key format (alphabetically ordered),
/// matching the CorrelationCalculator output.
/// </summary>
public static class CorrelationFilter
{
    public const decimal DefaultBlockThreshold = 0.7m;
    public const decimal DefaultReduceThreshold = 0.5m;

    /// <summary>
    /// Check if a candidate symbol should be blocked or reduced based on
    /// its correlation with existing open positions.
    /// </summary>
    /// <param name="candidateSymbol">Symbol being considered for entry.</param>
    /// <param name="openPositionSymbols">Symbols currently held in the portfolio.</param>
    /// <param name="pairwiseCorrelations">Correlation lookup with "A|B" keys (alphabetically ordered).</param>
    /// <param name="blockThreshold">Block entry above this avg correlation (default 0.7).</param>
    /// <param name="reduceThreshold">Start reducing size above this avg correlation (default 0.5).</param>
    /// <returns>Check result with action, correlations, and size multiplier.</returns>
    public static CorrelationCheckResult Check(
        string candidateSymbol,
        IReadOnlyList<string> openPositionSymbols,
        IReadOnlyDictionary<string, decimal> pairwiseCorrelations,
        decimal blockThreshold = DefaultBlockThreshold,
        decimal reduceThreshold = DefaultReduceThreshold)
    {
        // No open positions → always pass
        if (openPositionSymbols.Count == 0)
        {
            return new CorrelationCheckResult(
                Action: CorrelationAction.Pass,
                AvgCorrelation: 0m,
                MaxCorrelation: 0m,
                SizeMultiplier: 1m,
                PositionsChecked: 0,
                Detail: "No open positions; correlation check skipped");
        }

        // Look up correlations between candidate and each open position
        var correlations = new List<decimal>();
        foreach (var openSymbol in openPositionSymbols)
        {
            var key = MakeKey(candidateSymbol, openSymbol);
            if (pairwiseCorrelations.TryGetValue(key, out var corr))
            {
                correlations.Add(Math.Abs(corr)); // Use absolute correlation
            }
            // If no data for a pair, treat as uncorrelated (0) — don't penalize missing data
        }

        if (correlations.Count == 0)
        {
            return new CorrelationCheckResult(
                Action: CorrelationAction.Pass,
                AvgCorrelation: 0m,
                MaxCorrelation: 0m,
                SizeMultiplier: 1m,
                PositionsChecked: openPositionSymbols.Count,
                Detail: $"No correlation data for {candidateSymbol} vs {openPositionSymbols.Count} open positions; defaulting to Pass");
        }

        var avgCorr = correlations.Average();
        var maxCorr = correlations.Max();

        // Block: avg correlation exceeds block threshold
        if (avgCorr > blockThreshold)
        {
            return new CorrelationCheckResult(
                Action: CorrelationAction.Block,
                AvgCorrelation: Math.Round(avgCorr, 4),
                MaxCorrelation: Math.Round(maxCorr, 4),
                SizeMultiplier: 0m,
                PositionsChecked: correlations.Count,
                Detail: $"Blocked: avg correlation {avgCorr:F4} > {blockThreshold} threshold across {correlations.Count} positions");
        }

        // Reduce: avg correlation between reduce and block thresholds
        if (avgCorr >= reduceThreshold)
        {
            // Linear interpolation: at reduceThreshold → multiplier=1, at blockThreshold → multiplier=0
            var range = blockThreshold - reduceThreshold;
            var multiplier = range > 0
                ? Math.Max(0m, 1m - (avgCorr - reduceThreshold) / range)
                : 0m;

            return new CorrelationCheckResult(
                Action: CorrelationAction.Reduce,
                AvgCorrelation: Math.Round(avgCorr, 4),
                MaxCorrelation: Math.Round(maxCorr, 4),
                SizeMultiplier: Math.Round(multiplier, 4),
                PositionsChecked: correlations.Count,
                Detail: $"Reduced: avg correlation {avgCorr:F4} in [{reduceThreshold}, {blockThreshold}] → size multiplier {multiplier:F4}");
        }

        // Pass: low correlation
        return new CorrelationCheckResult(
            Action: CorrelationAction.Pass,
            AvgCorrelation: Math.Round(avgCorr, 4),
            MaxCorrelation: Math.Round(maxCorr, 4),
            SizeMultiplier: 1m,
            PositionsChecked: correlations.Count,
            Detail: $"Passed: avg correlation {avgCorr:F4} < {reduceThreshold} threshold");
    }

    /// <summary>
    /// Apply the correlation filter result to a share count.
    /// </summary>
    public static int AdjustShares(int shares, CorrelationCheckResult result)
    {
        if (result.Action == CorrelationAction.Block)
            return 0;

        if (result.Action == CorrelationAction.Reduce)
            return (int)(shares * result.SizeMultiplier);

        return shares;
    }

    /// <summary>
    /// Build a canonical lookup key for a symbol pair (alphabetically ordered).
    /// </summary>
    internal static string MakeKey(string symbolA, string symbolB)
    {
        return string.Compare(symbolA, symbolB, StringComparison.Ordinal) <= 0
            ? $"{symbolA}|{symbolB}"
            : $"{symbolB}|{symbolA}";
    }
}
