namespace TradingAssistant.Contracts.DTOs;

public record TradeNoteDto(
    Guid Id,
    Guid? OrderId,
    Guid? PositionId,
    string Content,
    List<string> Tags,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
