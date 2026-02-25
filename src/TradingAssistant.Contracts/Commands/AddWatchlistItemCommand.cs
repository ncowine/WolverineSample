namespace TradingAssistant.Contracts.Commands;

public record AddWatchlistItemCommand(Guid WatchlistId, string Symbol);
