namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Get the currently active ML model for a market.
/// </summary>
public record GetActiveMlModelQuery(string MarketCode);
