namespace TradingAssistant.Contracts.DTOs;

public record ActiveStrategyDto(
    Guid EntryId,
    Guid StrategyId,
    string StrategyName,
    string MarketCode,
    decimal AllocationPercent,
    decimal TotalReturn,
    decimal SharpeRatio,
    DateTime PromotedAt);
