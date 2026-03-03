namespace TradingAssistant.Contracts.Events;

public record StrategyRetired(
    Guid EntryId,
    Guid StrategyId,
    string StrategyName,
    string MarketCode,
    string RetirementReason,
    DateTime RetiredAt);
