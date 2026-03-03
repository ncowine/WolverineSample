namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Result of rule discovery analysis including Claude's identified patterns.
/// Recommendations are presented for user approval — not auto-applied.
/// </summary>
public record DiscoverRulesResultDto(
    bool Success,
    Guid? DiscoveryId,
    string StrategyName,
    int TradeCount,
    IReadOnlyList<DiscoveredRuleDto> DiscoveredRules,
    IReadOnlyList<string> Patterns,
    string Summary,
    string? Error = null);

/// <summary>
/// A single rule discovered from trade history analysis.
/// </summary>
public record DiscoveredRuleDto(
    string Rule,
    decimal Confidence,
    int SupportingTradeCount,
    string Description);
