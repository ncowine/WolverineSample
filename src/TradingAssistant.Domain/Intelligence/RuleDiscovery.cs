using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Stores rules discovered by Claude from analyzing trade history.
/// Links to a strategy (by Guid). Recommendations require user approval before applying.
/// </summary>
public class RuleDiscovery : BaseEntity
{
    public Guid StrategyId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public string MarketCode { get; set; } = string.Empty;

    /// <summary>Number of closed trades analyzed.</summary>
    public int TradeCount { get; set; }

    /// <summary>Number of winning trades in the analyzed set.</summary>
    public int WinningTrades { get; set; }

    /// <summary>Number of losing trades in the analyzed set.</summary>
    public int LosingTrades { get; set; }

    /// <summary>JSON array of DiscoveredRule objects (rule, confidence, supportingTradeCount, description).</summary>
    public string DiscoveredRulesJson { get; set; } = "[]";

    /// <summary>JSON array of pattern strings identified by Claude.</summary>
    public string PatternsJson { get; set; } = "[]";

    /// <summary>Human-readable summary from Claude.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Whether the user has approved these recommendations.</summary>
    public bool IsApproved { get; set; }

    /// <summary>When the discovery was performed.</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
