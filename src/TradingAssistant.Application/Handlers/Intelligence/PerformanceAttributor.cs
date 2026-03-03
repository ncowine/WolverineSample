using TradingAssistant.Domain.Intelligence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Pure static calculator for monthly performance attribution.
/// Decomposes returns into alpha, beta, regime, and residual components.
/// </summary>
public static class PerformanceAttributor
{
    /// <summary>Benchmark symbol mapping per market code.</summary>
    public static readonly IReadOnlyDictionary<string, string> BenchmarkSymbols =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["US_SP500"] = "SPY",
            ["IN_NIFTY50"] = "^NSEI"
        };

    public record MonthlyReturn(int Year, int Month, decimal StrategyReturn, decimal BenchmarkReturn);

    public record AttributionResult(
        int Year,
        int Month,
        decimal TotalReturn,
        decimal Alpha,
        decimal BetaContribution,
        decimal RegimeContribution,
        decimal Residual,
        decimal Beta,
        decimal BenchmarkReturn,
        int TradeCount,
        int RegimeAlignedTrades,
        int RegimeMismatchedTrades);

    public record RollingSummary(
        int MonthsIncluded,
        decimal CumulativeReturn,
        decimal CumulativeAlpha,
        decimal CumulativeBetaContribution,
        decimal CumulativeRegimeContribution,
        decimal CumulativeResidual,
        decimal AverageBeta,
        IReadOnlyList<AttributionResult> MonthlyResults);

    /// <summary>
    /// Compute beta from a history of monthly returns.
    /// Beta = Cov(strategy, benchmark) / Var(benchmark).
    /// Returns 0 if insufficient data or zero variance.
    /// </summary>
    public static decimal ComputeBeta(IReadOnlyList<MonthlyReturn> history)
    {
        if (history.Count < 2)
            return 0m;

        var n = history.Count;
        var stratReturns = history.Select(h => h.StrategyReturn).ToList();
        var benchReturns = history.Select(h => h.BenchmarkReturn).ToList();

        var stratMean = stratReturns.Average();
        var benchMean = benchReturns.Average();

        decimal covariance = 0m;
        decimal variance = 0m;

        for (int i = 0; i < n; i++)
        {
            var sd = stratReturns[i] - stratMean;
            var bd = benchReturns[i] - benchMean;
            covariance += sd * bd;
            variance += bd * bd;
        }

        // Use sample covariance/variance (n-1)
        covariance /= (n - 1);
        variance /= (n - 1);

        if (variance == 0)
            return 0m;

        var beta = covariance / variance;

        // Clamp to reasonable range
        return Math.Clamp(beta, -3m, 3m);
    }

    /// <summary>
    /// Attribute a single month's return into components.
    /// </summary>
    public static AttributionResult Attribute(
        int year,
        int month,
        decimal strategyReturn,
        decimal benchmarkReturn,
        decimal beta,
        decimal regimeContribution,
        int tradeCount,
        int regimeAlignedTrades,
        int regimeMismatchedTrades)
    {
        var betaContribution = beta * benchmarkReturn;
        var alpha = strategyReturn - betaContribution - regimeContribution;
        var residual = strategyReturn - alpha - betaContribution - regimeContribution;

        return new AttributionResult(
            Year: year,
            Month: month,
            TotalReturn: strategyReturn,
            Alpha: Math.Round(alpha, 4),
            BetaContribution: Math.Round(betaContribution, 4),
            RegimeContribution: Math.Round(regimeContribution, 4),
            Residual: Math.Round(residual, 4),
            Beta: Math.Round(beta, 4),
            BenchmarkReturn: benchmarkReturn,
            TradeCount: tradeCount,
            RegimeAlignedTrades: regimeAlignedTrades,
            RegimeMismatchedTrades: regimeMismatchedTrades);
    }

    /// <summary>
    /// Compute regime contribution from trade-level data.
    /// Regime-aligned = trades where entry and exit regime matched (stable).
    /// Regime-mismatched = trades where regime changed during the trade.
    /// Contribution = excess return from regime-aligned trades above average.
    /// </summary>
    public static decimal ComputeRegimeContribution(IReadOnlyList<TradeReview> monthTrades)
    {
        if (monthTrades.Count == 0)
            return 0m;

        var aligned = monthTrades
            .Where(t => !string.IsNullOrWhiteSpace(t.RegimeAtEntry)
                && string.Equals(t.RegimeAtEntry.Trim(), t.RegimeAtExit.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        var mismatched = monthTrades
            .Where(t => !string.IsNullOrWhiteSpace(t.RegimeAtEntry)
                && !string.IsNullOrWhiteSpace(t.RegimeAtExit)
                && !string.Equals(t.RegimeAtEntry.Trim(), t.RegimeAtExit.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (aligned.Count == 0 && mismatched.Count == 0)
            return 0m;

        // Regime contribution = performance difference between aligned and mismatched trades
        var alignedAvg = aligned.Count > 0 ? aligned.Average(t => t.PnlPercent) : 0m;
        var mismatchedAvg = mismatched.Count > 0 ? mismatched.Average(t => t.PnlPercent) : 0m;

        if (mismatched.Count == 0)
            return 0m; // No mismatch to compare against

        // Contribution is proportional to the count of mismatched trades
        // and the performance gap between aligned and mismatched
        return (alignedAvg - mismatchedAvg) * mismatched.Count / monthTrades.Count;
    }

    /// <summary>
    /// Build a rolling 12-month attribution summary.
    /// </summary>
    public static RollingSummary BuildRollingSummary(IReadOnlyList<AttributionResult> results)
    {
        if (results.Count == 0)
        {
            return new RollingSummary(0, 0, 0, 0, 0, 0, 0, []);
        }

        // Take the most recent 12 months
        var recent = results
            .OrderByDescending(r => r.Year * 100 + r.Month)
            .Take(12)
            .OrderBy(r => r.Year * 100 + r.Month)
            .ToList();

        return new RollingSummary(
            MonthsIncluded: recent.Count,
            CumulativeReturn: recent.Sum(r => r.TotalReturn),
            CumulativeAlpha: recent.Sum(r => r.Alpha),
            CumulativeBetaContribution: recent.Sum(r => r.BetaContribution),
            CumulativeRegimeContribution: recent.Sum(r => r.RegimeContribution),
            CumulativeResidual: recent.Sum(r => r.Residual),
            AverageBeta: recent.Average(r => r.Beta),
            MonthlyResults: recent);
    }
}
