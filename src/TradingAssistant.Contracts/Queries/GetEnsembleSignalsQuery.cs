namespace TradingAssistant.Contracts.Queries;

public record GetEnsembleSignalsQuery(string MarketCode, DateTime? Date = null);
