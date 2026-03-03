namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Retrieve rule discovery history for a strategy.
/// </summary>
public record GetDiscoveredRulesQuery(Guid StrategyId);
