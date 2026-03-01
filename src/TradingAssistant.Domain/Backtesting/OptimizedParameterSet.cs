using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

/// <summary>
/// A versioned set of optimized parameters produced by walk-forward analysis.
/// The screener uses the latest active set for its strategy.
/// </summary>
public class OptimizedParameterSet : BaseEntity
{
    public Guid StrategyId { get; set; }

    /// <summary>
    /// JSON-serialized Dictionary&lt;string, decimal&gt; of parameter values.
    /// </summary>
    public string ParametersJson { get; set; } = "{}";

    // ── Walk-Forward Metrics ────────────────────────────────────

    /// <summary>
    /// Average out-of-sample Sharpe ratio across walk-forward windows.
    /// </summary>
    public decimal AvgOutOfSampleSharpe { get; set; }

    /// <summary>
    /// Average walk-forward efficiency (OOS/IS Sharpe). > 0.5 is acceptable.
    /// </summary>
    public decimal AvgEfficiency { get; set; }

    /// <summary>
    /// Average overfitting score across windows. Lower is better.
    /// </summary>
    public decimal AvgOverfittingScore { get; set; }

    /// <summary>
    /// Overfitting grade: Good, Warning, Overfitted.
    /// </summary>
    public string OverfittingGrade { get; set; } = "Good";

    /// <summary>
    /// Number of walk-forward windows used.
    /// </summary>
    public int WindowCount { get; set; }

    /// <summary>
    /// Version number (1-based, incrementing per strategy).
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// True if this is the current active parameter set for the strategy.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation
    public Strategy Strategy { get; set; } = null!;
}
