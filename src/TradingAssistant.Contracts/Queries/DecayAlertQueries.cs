namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Get active (unresolved) decay alerts, optionally filtered by market.
/// </summary>
public record GetDecayAlertsQuery(string? MarketCode = null, bool IncludeResolved = false);
