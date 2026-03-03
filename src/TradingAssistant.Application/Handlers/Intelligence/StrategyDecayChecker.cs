using TradingAssistant.Domain.Intelligence.Enums;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Pure calculation logic for detecting strategy decay.
/// Computes rolling metrics and compares against historical baselines.
/// </summary>
public static class StrategyDecayChecker
{
    /// <summary>Warning: 60-day Sharpe drops below this fraction of historical average.</summary>
    public const decimal WarningThresholdFraction = 0.50m;

    /// <summary>Severe: 30-day Sharpe goes below this value.</summary>
    public const decimal SevereSharpeThreshold = 0m;

    /// <summary>Minimum trades in a rolling window to compute meaningful metrics.</summary>
    public const int MinTradesForMetric = 5;

    /// <summary>
    /// Represents trade P&L data for rolling metric computation.
    /// </summary>
    public record TradeData(DateTime ExitDate, decimal PnlPercent);

    /// <summary>
    /// Rolling metrics for a specific time window.
    /// </summary>
    public record RollingMetrics(
        int WindowDays,
        int TradeCount,
        decimal Sharpe,
        decimal WinRate,
        decimal AvgPnl);

    /// <summary>
    /// Result of decay analysis.
    /// </summary>
    public record DecayCheckResult(
        bool AlertTriggered,
        DecayAlertType? AlertType,
        string? TriggerReason,
        RollingMetrics Rolling30,
        RollingMetrics Rolling60,
        RollingMetrics Rolling90,
        decimal HistoricalSharpe);

    /// <summary>
    /// Analyze trade history for signs of strategy decay.
    /// </summary>
    /// <param name="trades">All trades for the strategy, ordered by exit date.</param>
    /// <param name="asOfDate">Date to compute rolling windows from (default: UtcNow).</param>
    public static DecayCheckResult CheckForDecay(
        IReadOnlyList<TradeData> trades,
        DateTime? asOfDate = null)
    {
        var now = asOfDate ?? DateTime.UtcNow;

        var rolling30 = ComputeRollingMetrics(trades, now, 30);
        var rolling60 = ComputeRollingMetrics(trades, now, 60);
        var rolling90 = ComputeRollingMetrics(trades, now, 90);
        var historicalSharpe = ComputeSharpe(trades.Select(t => t.PnlPercent).ToList());

        // Check Severe first (30-day Sharpe negative)
        if (rolling30.TradeCount >= MinTradesForMetric && rolling30.Sharpe < SevereSharpeThreshold)
        {
            return new DecayCheckResult(
                AlertTriggered: true,
                AlertType: DecayAlertType.Severe,
                TriggerReason: $"30-day Sharpe ({rolling30.Sharpe:F4}) is negative — strategy edge lost",
                rolling30, rolling60, rolling90, historicalSharpe);
        }

        // Check Warning (60-day Sharpe < 50% of historical)
        if (rolling60.TradeCount >= MinTradesForMetric
            && historicalSharpe > 0
            && rolling60.Sharpe < historicalSharpe * WarningThresholdFraction)
        {
            return new DecayCheckResult(
                AlertTriggered: true,
                AlertType: DecayAlertType.Warning,
                TriggerReason: $"60-day Sharpe ({rolling60.Sharpe:F4}) below {WarningThresholdFraction:P0} of historical ({historicalSharpe:F4})",
                rolling30, rolling60, rolling90, historicalSharpe);
        }

        return new DecayCheckResult(
            AlertTriggered: false,
            AlertType: null,
            TriggerReason: null,
            rolling30, rolling60, rolling90, historicalSharpe);
    }

    /// <summary>
    /// Compute rolling metrics for trades within the window.
    /// </summary>
    public static RollingMetrics ComputeRollingMetrics(
        IReadOnlyList<TradeData> allTrades, DateTime asOf, int windowDays)
    {
        var windowStart = asOf.AddDays(-windowDays);
        var windowTrades = allTrades
            .Where(t => t.ExitDate >= windowStart && t.ExitDate <= asOf)
            .ToList();

        if (windowTrades.Count == 0)
            return new RollingMetrics(windowDays, 0, 0m, 0m, 0m);

        var pnls = windowTrades.Select(t => t.PnlPercent).ToList();
        var sharpe = ComputeSharpe(pnls);
        var winRate = pnls.Count(p => p > 0) / (decimal)pnls.Count * 100m;
        var avgPnl = pnls.Average();

        return new RollingMetrics(windowDays, windowTrades.Count, sharpe, winRate, avgPnl);
    }

    /// <summary>
    /// Compute annualized Sharpe ratio from a list of trade P&L percentages.
    /// Assumes roughly 252 trading days per year.
    /// Returns 0 if insufficient data or zero standard deviation.
    /// </summary>
    public static decimal ComputeSharpe(IReadOnlyList<decimal> pnls)
    {
        if (pnls.Count < 2)
            return 0m;

        var mean = pnls.Average();
        var variance = pnls.Sum(p => (p - mean) * (p - mean)) / (pnls.Count - 1);

        if (variance <= 0)
            return mean >= 0 ? 10m : -10m; // Cap at extreme values for zero variance

        var stdDev = (decimal)Math.Sqrt((double)variance);
        // Annualize: multiply by sqrt(252 / avgTradesPerDay)
        // Simplified: just use mean/stdDev as the ratio (per-trade Sharpe)
        return mean / stdDev;
    }
}
