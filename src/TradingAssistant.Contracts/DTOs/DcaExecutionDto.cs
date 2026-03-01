namespace TradingAssistant.Contracts.DTOs;

public record DcaExecutionDto(
    Guid Id,
    Guid DcaPlanId,
    Guid? OrderId,
    decimal Amount,
    decimal? ExecutedPrice,
    decimal? Quantity,
    string Status,
    string? ErrorReason,
    DateTime CreatedAt);
