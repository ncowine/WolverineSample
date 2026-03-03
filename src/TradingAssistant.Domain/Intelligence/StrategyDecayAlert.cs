using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Alert raised when a strategy's rolling performance degrades beyond thresholds.
/// Warning: 60-day Sharpe drops below 50% of historical average.
/// Severe: 30-day Sharpe goes negative.
/// </summary>
public class StrategyDecayAlert : BaseEntity
{
    public Guid StrategyId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;

    public DecayAlertType AlertType { get; set; }

    // --- Rolling Metrics ---
    public decimal Rolling30DaySharpe { get; set; }
    public decimal Rolling60DaySharpe { get; set; }
    public decimal Rolling90DaySharpe { get; set; }

    public decimal Rolling30DayWinRate { get; set; }
    public decimal Rolling60DayWinRate { get; set; }
    public decimal Rolling90DayWinRate { get; set; }

    public decimal Rolling30DayAvgPnl { get; set; }
    public decimal Rolling60DayAvgPnl { get; set; }
    public decimal Rolling90DayAvgPnl { get; set; }

    // --- Baseline ---
    /// <summary>Historical average Sharpe for this strategy (computed from all trades).</summary>
    public decimal HistoricalSharpe { get; set; }

    /// <summary>Description of which threshold triggered the alert.</summary>
    public string TriggerReason { get; set; } = string.Empty;

    // --- Claude Analysis ---
    /// <summary>Claude's analysis of the probable cause of decay.</summary>
    public string? ClaudeAnalysis { get; set; }

    // --- Resolution ---
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime AlertedAt { get; set; } = DateTime.UtcNow;
}
