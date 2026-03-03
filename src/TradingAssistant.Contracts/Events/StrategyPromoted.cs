namespace TradingAssistant.Contracts.Events;

public record StrategyPromoted(
    Guid EntryId,
    Guid StrategyId,
    string StrategyName,
    string MarketCode,
    decimal AllocationPercent,
    DateTime PromotedAt);
