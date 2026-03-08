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

    // ── Extended metrics ────────────────────────────────

    public string EquityCurveJson { get; set; } = string.Empty;
    public string TradeLogJson { get; set; } = string.Empty;
    public string MonthlyReturnsJson { get; set; } = "{}";
    public string BenchmarkReturnJson { get; set; } = "{}";
    public string ParametersJson { get; set; } = "{}";
    public string WalkForwardJson { get; set; } = "{}";
    public decimal? OverfittingScore { get; set; }
    public string SpyComparisonJson { get; set; } = "{}";

    // ── Performance metrics ─────────────────────────────

    public decimal Cagr { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal CalmarRatio { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }

    // ── Portfolio-specific metrics (nullable for single-symbol) ──

    public int? UniqueSymbolsTraded { get; set; }
    public decimal? AveragePositionsHeld { get; set; }
    public int? MaxPositionsHeld { get; set; }
    public string? SymbolBreakdownJson { get; set; }
    public string? ExecutionLogJson { get; set; }
    public string? RegimeTimelineJson { get; set; }

    public BacktestRun BacktestRun { get; set; } = null!;
}
