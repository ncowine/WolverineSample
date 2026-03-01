namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Comprehensive performance metrics computed from a backtest result.
/// </summary>
public class PerformanceMetrics
{
    // ── Return Metrics ────────────────────────────────────────
    public decimal TotalReturn { get; init; }
    public decimal Cagr { get; init; }
    public decimal AnnualizedVolatility { get; init; }

    // ── Risk-Adjusted Metrics ─────────────────────────────────
    public decimal SharpeRatio { get; init; }
    public decimal SortinoRatio { get; init; }
    public decimal CalmarRatio { get; init; }

    // ── Drawdown Metrics ──────────────────────────────────────
    public decimal MaxDrawdownPercent { get; init; }
    public int MaxDrawdownDurationDays { get; init; }

    // ── Trade Statistics ──────────────────────────────────────
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public decimal WinRate { get; init; }
    public decimal ProfitFactor { get; init; }
    public decimal Expectancy { get; init; }
    public decimal AverageWin { get; init; }
    public decimal AverageLoss { get; init; }
    public decimal LargestWin { get; init; }
    public decimal LargestLoss { get; init; }
    public decimal AverageHoldingDays { get; init; }

    // ── Benchmark Comparison ──────────────────────────────────
    public decimal BenchmarkReturn { get; init; }
    public decimal BenchmarkCagr { get; init; }
    public decimal Alpha { get; init; }
    public decimal Beta { get; init; }

    // ── Monthly/Yearly Returns ────────────────────────────────
    /// <summary>
    /// Monthly returns: key = "2025-01", value = return %.
    /// </summary>
    public Dictionary<string, decimal> MonthlyReturns { get; init; } = new();

    /// <summary>
    /// Yearly returns: key = 2025, value = return %.
    /// </summary>
    public Dictionary<int, decimal> YearlyReturns { get; init; } = new();

    // ── Red Flags ─────────────────────────────────────────────
    public List<string> RedFlags { get; init; } = new();
}
