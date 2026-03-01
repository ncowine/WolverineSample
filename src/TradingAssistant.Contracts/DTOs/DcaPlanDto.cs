namespace TradingAssistant.Contracts.DTOs;

public record DcaPlanDto(
    Guid Id,
    Guid AccountId,
    string Symbol,
    decimal Amount,
    string Frequency,
    DateTime NextExecutionDate,
    bool IsActive,
    DateTime CreatedAt);
