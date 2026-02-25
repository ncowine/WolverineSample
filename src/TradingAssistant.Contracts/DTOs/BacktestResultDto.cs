namespace TradingAssistant.Contracts.DTOs;

public record BacktestResultDto(
    Guid Id,
    Guid BacktestRunId,
    Guid StrategyId,
    string Symbol,
    string Status,
    int TotalTrades,
    decimal WinRate,
    decimal TotalReturn,
    decimal MaxDrawdown,
    decimal SharpeRatio,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt);
