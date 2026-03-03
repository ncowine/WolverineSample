namespace TradingAssistant.Contracts.DTOs;

/// <summary>
/// Summary DTO for a stored strategy autopsy record.
/// </summary>
public record StrategyAutopsyDto(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    string MarketCode,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal MonthlyReturnPercent,
    string PrimaryLossReason,
    IReadOnlyList<string> RootCauses,
    string MarketConditionImpact,
    IReadOnlyList<string> Recommendations,
    bool ShouldRetire,
    decimal Confidence,
    string Summary,
    DateTime AnalyzedAt);
