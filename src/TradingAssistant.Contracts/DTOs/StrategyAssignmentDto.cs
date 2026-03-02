namespace TradingAssistant.Contracts.DTOs;

public record StrategyAssignmentDto(
    Guid Id,
    string MarketCode,
    Guid StrategyId,
    string StrategyName,
    string Regime,
    decimal AllocationPercent,
    bool IsLocked,
    DateTime SwitchoverStartDate,
    DateTime AssignedAt);
