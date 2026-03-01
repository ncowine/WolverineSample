namespace TradingAssistant.Contracts.DTOs;

public record PositionDto(
    Guid Id,
    Guid AccountId,
    string Symbol,
    decimal Quantity,
    decimal AverageEntryPrice,
    decimal CurrentPrice,
    decimal UnrealizedPnL,
    string Status,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string AccountType);
