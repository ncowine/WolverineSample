namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Configuration for a backtest run.
/// </summary>
public class BacktestConfig
{
    public decimal InitialCapital { get; init; } = 100_000m;
    public decimal SlippagePercent { get; init; } = 0.1m;
    public decimal CommissionPerTrade { get; init; } = 1m;
    public decimal RiskFreeRate { get; init; } = 4.5m; // annual %

    public static BacktestConfig Default => new();
}
