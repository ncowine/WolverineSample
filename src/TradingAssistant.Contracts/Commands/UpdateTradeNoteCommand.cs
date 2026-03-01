namespace TradingAssistant.Contracts.Commands;

public record UpdateTradeNoteCommand(Guid NoteId, string Content, List<string>? Tags = null);
