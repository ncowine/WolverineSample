namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Check if a strategy is showing signs of decay based on recent trade performance.
/// </summary>
public record CheckDecayCommand(Guid StrategyId, string MarketCode);

/// <summary>
/// Resolve (acknowledge/dismiss) a decay alert.
/// </summary>
public record ResolveDecayAlertCommand(Guid AlertId, string? Note = null);
