namespace TradingAssistant.Contracts.DTOs;

public record BacktestRunDto(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    string Symbol,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    DateTime CreatedAt);
