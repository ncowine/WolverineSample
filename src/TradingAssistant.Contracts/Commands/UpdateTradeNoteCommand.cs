namespace TradingAssistant.Contracts.Commands;

public record UpdateTradeNoteCommand(Guid NoteId, string Content);
