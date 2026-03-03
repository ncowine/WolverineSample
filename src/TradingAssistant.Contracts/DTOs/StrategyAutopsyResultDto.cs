namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Result of a strategy autopsy including Claude's analysis.
/// </summary>
public record StrategyAutopsyResultDto(
    bool Success,
    Guid? AutopsyId,
    string StrategyName,
    string PrimaryLossReason,
    IReadOnlyList<string> RootCauses,
    string MarketConditionImpact,
    IReadOnlyList<string> Recommendations,
    bool ShouldRetire,
    decimal Confidence,
    string Summary,
    string? Error = null);
