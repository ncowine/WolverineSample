using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Periodic pattern report generated every 50 closed trades per market.
/// Captures mistake distribution, regime breakdown, and recommendations.
/// </summary>
public class MistakePatternReport : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;

    /// <summary>Total trades analyzed in this report window.</summary>
    public int TradeCount { get; set; }

    /// <summary>Number of losing trades in the window.</summary>
    public int LosingTradeCount { get; set; }

    /// <summary>Most frequently occurring mistake type.</summary>
    public string MostCommonMistake { get; set; } = string.Empty;

    /// <summary>JSON dictionary: MistakeType → count.</summary>
    public string MistakeBreakdownJson { get; set; } = "{}";

    /// <summary>JSON dictionary: regime → { MistakeType → count }.</summary>
    public string RegimeBreakdownJson { get; set; } = "{}";

    /// <summary>JSON array of recommendation strings.</summary>
    public string RecommendationsJson { get; set; } = "[]";

    /// <summary>Optional Claude-generated analysis of the patterns.</summary>
    public string? ClaudeAnalysis { get; set; }

    /// <summary>When this report was generated.</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}
