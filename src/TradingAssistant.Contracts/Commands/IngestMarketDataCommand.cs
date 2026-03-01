namespace TradingAssistant.Contracts.Commands;

public record IngestMarketDataCommand(string Symbol, int YearsBack = 5);
