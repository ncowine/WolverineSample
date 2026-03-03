namespace TradingAssistant.Contracts.Queries;

/// <summary>
/// Get monthly performance attribution for a specific market and month.
/// </summary>
public record GetAttributionQuery(string MarketCode, int Year, int Month);

/// <summary>
/// Get rolling 12-month attribution summary for a market.
/// </summary>
public record GetRollingAttributionQuery(string MarketCode);
