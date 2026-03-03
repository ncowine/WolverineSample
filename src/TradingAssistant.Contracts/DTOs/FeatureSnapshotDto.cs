namespace TradingAssistant.Contracts.DTOs;

public record FeatureSnapshotDto(
    Guid Id,
    Guid TradeId,
    string Symbol,
    string MarketCode,
    DateTime CapturedAt,
    int FeatureVersion,
    int FeatureCount,
    string TradeOutcome,
    decimal? TradePnlPercent,
    DateTime? OutcomeUpdatedAt,
    IReadOnlyDictionary<string, object>? Features = null);
