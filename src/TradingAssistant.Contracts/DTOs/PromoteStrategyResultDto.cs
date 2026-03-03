namespace TradingAssistant.Contracts.DTOs;

public record PromoteStrategyResultDto(
    bool Success,
    Guid EntryId,
    string StrategyName,
    decimal AllocationPercent,
    string? Reason = null,
    string? Error = null);
