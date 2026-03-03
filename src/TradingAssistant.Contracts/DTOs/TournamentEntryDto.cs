namespace TradingAssistant.Contracts.DTOs;

public record TournamentEntryDto(
    Guid Id,
    Guid TournamentRunId,
    Guid StrategyId,
    Guid PaperAccountId,
    string MarketCode,
    DateTime StartDate,
    int DaysActive,
    int TotalTrades,
    decimal WinRate,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    decimal TotalReturn,
    string Status,
    decimal AllocationPercent);
