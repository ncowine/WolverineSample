namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Run ML prediction for a symbol in a market.
/// Uses the active model and current feature snapshot to produce a win probability.
/// </summary>
public record PredictConfidenceCommand(string MarketCode, string Symbol);
