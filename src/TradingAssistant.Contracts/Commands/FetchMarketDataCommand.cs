namespace TradingAssistant.Contracts.Commands;

public record FetchMarketDataCommand(
    string Symbol,
    DateTime? From = null,
    DateTime? To = null);
