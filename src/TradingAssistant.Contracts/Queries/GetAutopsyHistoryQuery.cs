namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Retrieve autopsy history for a strategy.
/// </summary>
public record GetAutopsyHistoryQuery(Guid StrategyId);
