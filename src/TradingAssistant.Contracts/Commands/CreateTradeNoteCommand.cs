namespace TradingAssistant.Contracts.Commands;

public record CreateTradeNoteCommand(Guid? OrderId, Guid? PositionId, string Content);
