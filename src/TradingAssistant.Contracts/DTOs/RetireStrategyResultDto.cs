namespace TradingAssistant.Contracts.DTOs;

public record RetireStrategyResultDto(
    bool Success,
    Guid EntryId,
    string StrategyName,
    string RetirementReason,
    string? Error = null);
