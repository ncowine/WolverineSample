namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Analyze a strategy's trade history to discover patterns that distinguish winners from losers.
/// Requires minimum 50 closed trades. Uses Claude AI for pattern recognition.
/// </summary>
public record DiscoverRulesCommand(
    Guid StrategyId,
    string MarketCode = "US_SP500");
