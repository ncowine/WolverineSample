namespace TradingAssistant.Contracts.DTOs;

public record StrategyDecayAlertDto(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    string MarketCode,
    string AlertType,
    decimal Rolling30DaySharpe,
    decimal Rolling60DaySharpe,
    decimal Rolling90DaySharpe,
    decimal Rolling30DayWinRate,
    decimal Rolling60DayWinRate,
    decimal Rolling90DayWinRate,
    decimal Rolling30DayAvgPnl,
    decimal Rolling60DayAvgPnl,
    decimal Rolling90DayAvgPnl,
    decimal HistoricalSharpe,
    string TriggerReason,
    string? ClaudeAnalysis,
    bool IsResolved,
    DateTime? ResolvedAt,
    string? ResolutionNote,
    DateTime AlertedAt);

public record CheckDecayResultDto(
    bool AlertTriggered,
    Guid? AlertId,
    string? AlertType,
    string? TriggerReason,
    string? ClaudeAnalysis);
