namespace TradingAssistant.Contracts.DTOs;

public record TradeNoteDto(
    Guid Id,
    Guid? OrderId,
    Guid? PositionId,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
