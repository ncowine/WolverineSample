namespace TradingAssistant.Contracts.DTOs;

public record LeaderboardEntryDto(
    Guid EntryId,
    Guid StrategyId,
    string StrategyName,
    string MarketCode,
    int DaysActive,
    int TotalTrades,
    decimal WinRate,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    decimal TotalReturn,
    decimal AllocationPercent,
    string Status,
    bool EligibleForPromotion);
