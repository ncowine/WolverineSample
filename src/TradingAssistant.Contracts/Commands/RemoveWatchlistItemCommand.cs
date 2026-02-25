namespace TradingAssistant.Contracts.Commands;

public record RemoveWatchlistItemCommand(Guid WatchlistId, string Symbol);
