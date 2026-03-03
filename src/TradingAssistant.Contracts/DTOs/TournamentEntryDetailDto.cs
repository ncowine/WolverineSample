namespace TradingAssistant.Contracts.DTOs;

public record TournamentEntryDetailDto(
    Guid EntryId,
    Guid TournamentRunId,
    Guid StrategyId,
    string StrategyName,
    Guid PaperAccountId,
    string MarketCode,
    DateTime StartDate,
    int DaysActive,
    int TotalTrades,
    decimal WinRate,
    decimal SharpeRatio,
    decimal MaxDrawdown,
    decimal TotalReturn,
    decimal AllocationPercent,
    string Status,
    bool EligibleForPromotion,
    IReadOnlyList<EquityPointDto> EquityCurve);

public record EquityPointDto(string Date, decimal Value);
