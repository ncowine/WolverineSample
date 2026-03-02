namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Lock a strategy assignment so regime changes won't override it.
/// </summary>
public record LockStrategyCommand(string MarketCode, Guid StrategyId);

/// <summary>
/// Unlock a strategy assignment to allow automatic regime-based selection.
/// </summary>
public record UnlockStrategyCommand(string MarketCode);
