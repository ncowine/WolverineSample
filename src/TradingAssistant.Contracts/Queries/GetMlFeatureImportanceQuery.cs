namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Get top feature importance entries for the active ML model for a market.
/// </summary>
public record GetMlFeatureImportanceQuery(string MarketCode);
