using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Post-mortem analysis of a strategy's losing month, produced by Claude AI.
/// Links to a strategy (by Guid) and a specific month/year.
/// </summary>
public class StrategyAutopsy : BaseEntity
{
    public Guid StrategyId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;

    /// <summary>First day of the analyzed month.</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>Last day of the analyzed month.</summary>
    public DateTime PeriodEnd { get; set; }

    public decimal MonthlyReturnPercent { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal WinRate { get; set; }
    public decimal SharpeRatio { get; set; }
    public int TradeCount { get; set; }

    /// <summary>Primary classified reason for the loss.</summary>
    public LossReason PrimaryLossReason { get; set; }

    /// <summary>JSON array of root cause strings from Claude analysis.</summary>
    public string RootCausesJson { get; set; } = "[]";

    /// <summary>Claude's assessment of market condition impact.</summary>
    public string MarketConditionImpact { get; set; } = string.Empty;

    /// <summary>JSON array of actionable recommendation strings.</summary>
    public string RecommendationsJson { get; set; } = "[]";

    /// <summary>Whether Claude recommends retiring this strategy.</summary>
    public bool ShouldRetire { get; set; }

    /// <summary>Claude's confidence in the analysis (0.0–1.0).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Human-readable summary of the autopsy.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>When the autopsy was performed.</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
