namespace TradingAssistant.Contracts.Events;

public record RegimeChanged(
    string MarketCode,
    string FromRegime,
    string ToRegime,
    DateTime TransitionDate,
    decimal ConfidenceScore);
