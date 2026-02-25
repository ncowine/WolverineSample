namespace TradingAssistant.Contracts.DTOs;

public record OrderDto(
    Guid Id,
    Guid AccountId,
    string Symbol,
    string Side,
    string Type,
    decimal Quantity,
    decimal? Price,
    string Status,
    DateTime CreatedAt,
    DateTime? FilledAt);
