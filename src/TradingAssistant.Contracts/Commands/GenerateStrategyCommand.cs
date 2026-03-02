namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Generate a trading strategy from a natural language description using Claude AI.
/// The generated strategy is auto-backtested and rejected if walk-forward Sharpe &lt; threshold.
/// </summary>
public record GenerateStrategyCommand(
    string Description,
    string MarketCode,
    string RegimeType,
    decimal MaxDrawdownPercent = 15m,
    decimal TargetSharpe = 0.5m,
    string? AdditionalConstraints = null);
