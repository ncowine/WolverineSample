using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class BacktestResult : BaseEntity
{
    public Guid BacktestRunId { get; set; }
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal SharpeRatio { get; set; }
    public string ResultData { get; set; } = "{}"; // JSON for detailed trade-by-trade data

    // ── New fields (STORY-034) ────────────────────────────────

    /// <summary>
    /// Gzip-compressed JSON: daily equity values [{Date, Value}].
    /// </summary>
    public string EquityCurveJson { get; set; } = string.Empty;

    /// <summary>
    /// Gzip-compressed JSON: list of all trades (entry/exit date, price, P&amp;L, reason, holding period).
    /// </summary>
    public string TradeLogJson { get; set; } = string.Empty;

    /// <summary>
    /// JSON: matrix of month x year returns {"2024-01": 2.3, ...}.
    /// </summary>
    public string MonthlyReturnsJson { get; set; } = "{}";

    /// <summary>
    /// JSON: SPY equity curve for same period.
    /// </summary>
    public string BenchmarkReturnJson { get; set; } = "{}";

    /// <summary>
    /// JSON: strategy parameters used for this run.
    /// </summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>
    /// JSON: per-window walk-forward results (if from optimization).
    /// </summary>
    public string WalkForwardJson { get; set; } = "{}";

    /// <summary>
    /// Walk-forward overfitting score (lower is better). Null if not from optimization.
    /// </summary>
    public decimal? OverfittingScore { get; set; }

    /// <summary>
    /// JSON: SPY comparison data {StrategyCagr, SpyCagr, Alpha, Beta}.
    /// </summary>
    public string SpyComparisonJson { get; set; } = "{}";

    // ── Additional metrics from PerformanceCalculator ─────────

    public decimal Cagr { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal CalmarRatio { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }

    public BacktestRun BacktestRun { get; set; } = null!;
}
