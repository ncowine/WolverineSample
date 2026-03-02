namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Rolling trade statistics for Kelly calculation.
/// </summary>
public record TradeStats(
    int TotalTrades,
    int Winners,
    int Losers,
    decimal AvgWin,
    decimal AvgLoss,
    decimal WinRate,
    decimal PayoffRatio);

/// <summary>
/// Result of Kelly position sizing calculation.
/// </summary>
public record KellySizingResult(
    decimal KellyFraction,
    decimal AdjustedFraction,
    decimal RiskPercent,
    string Method,
    int TradesUsed);

/// <summary>
/// Pure static Kelly Criterion calculator for position sizing.
///
/// Kelly formula: f* = (W × R - L) / R
///   where W = win rate, L = loss rate (1 - W), R = payoff ratio (avg win / avg loss)
/// </summary>
public static class KellyCriterion
{
    public const int MinTradesForKelly = 30;
    public const int DefaultWindowSize = 50;
    public const decimal DefaultFallbackRiskPercent = 1m;
    public const decimal DefaultKellyMultiplier = 0.5m; // Half-Kelly

    /// <summary>
    /// Compute trade statistics from a sequence of P&amp;L values.
    /// Uses the most recent <paramref name="windowSize"/> trades.
    /// </summary>
    public static TradeStats ComputeStats(IReadOnlyList<decimal> pnls, int windowSize = DefaultWindowSize)
    {
        if (pnls.Count == 0)
            return new TradeStats(0, 0, 0, 0m, 0m, 0m, 0m);

        // Take the most recent N trades
        var window = pnls.Count <= windowSize
            ? pnls
            : pnls.Skip(pnls.Count - windowSize).ToList();

        var winners = window.Where(p => p > 0).ToList();
        var losers = window.Where(p => p <= 0).ToList();

        var totalTrades = window.Count;
        var winCount = winners.Count;
        var loseCount = losers.Count;
        var winRate = totalTrades > 0 ? (decimal)winCount / totalTrades : 0m;
        var avgWin = winners.Count > 0 ? winners.Average() : 0m;
        var avgLoss = losers.Count > 0 ? Math.Abs(losers.Average()) : 0m;
        var payoffRatio = avgLoss > 0 ? avgWin / avgLoss : 0m;

        return new TradeStats(totalTrades, winCount, loseCount, avgWin, avgLoss, winRate, payoffRatio);
    }

    /// <summary>
    /// Calculate raw Kelly fraction: f* = (W × R - L) / R
    /// Returns 0 if the formula produces a negative value (negative edge).
    /// </summary>
    public static decimal CalculateKellyFraction(decimal winRate, decimal payoffRatio)
    {
        if (payoffRatio <= 0 || winRate <= 0 || winRate >= 1)
            return 0m;

        var lossRate = 1m - winRate;
        var kelly = (winRate * payoffRatio - lossRate) / payoffRatio;

        return Math.Max(0m, kelly);
    }

    /// <summary>
    /// Full Kelly sizing: compute the risk percentage to apply per trade.
    ///
    /// Flow:
    /// 1. If &lt; MinTradesForKelly closed trades → fallback to fixed risk percent
    /// 2. Compute stats from rolling window
    /// 3. Calculate raw Kelly fraction
    /// 4. Apply fractional multiplier (default half-Kelly)
    /// 5. Clamp to [0, maxRiskPercent]
    /// 6. Apply min(Kelly risk, existing risk-per-trade, remaining heat budget)
    /// </summary>
    public static KellySizingResult CalculatePositionRisk(
        IReadOnlyList<decimal> tradePnls,
        decimal equity,
        decimal currentHeatPercent,
        decimal maxPortfolioHeat,
        decimal fixedRiskPercent = DefaultFallbackRiskPercent,
        decimal kellyMultiplier = DefaultKellyMultiplier,
        int windowSize = DefaultWindowSize,
        decimal maxRiskPercent = 5m)
    {
        // Fallback: not enough trade history
        if (tradePnls.Count < MinTradesForKelly)
        {
            return new KellySizingResult(
                KellyFraction: 0m,
                AdjustedFraction: 0m,
                RiskPercent: fixedRiskPercent,
                Method: "FixedFallback",
                TradesUsed: tradePnls.Count);
        }

        var stats = ComputeStats(tradePnls, windowSize);
        var rawKelly = CalculateKellyFraction(stats.WinRate, stats.PayoffRatio);

        // Apply fractional Kelly (e.g., half-Kelly)
        var adjustedKelly = rawKelly * kellyMultiplier;

        // Convert to risk percent and clamp
        var kellyRiskPercent = Math.Min(adjustedKelly * 100m, maxRiskPercent);

        // Zero or negative edge → fallback
        if (kellyRiskPercent <= 0)
        {
            return new KellySizingResult(
                KellyFraction: rawKelly,
                AdjustedFraction: adjustedKelly,
                RiskPercent: fixedRiskPercent,
                Method: "FixedFallback_NoEdge",
                TradesUsed: stats.TotalTrades);
        }

        // Constrain by remaining portfolio heat budget
        var remainingHeat = Math.Max(0, maxPortfolioHeat - currentHeatPercent);
        var constrainedRisk = Math.Min(kellyRiskPercent, remainingHeat);

        // Also constrain by the fixed risk cap
        constrainedRisk = Math.Min(constrainedRisk, fixedRiskPercent);

        return new KellySizingResult(
            KellyFraction: rawKelly,
            AdjustedFraction: adjustedKelly,
            RiskPercent: Math.Max(0, constrainedRisk),
            Method: "Kelly",
            TradesUsed: stats.TotalTrades);
    }
}
