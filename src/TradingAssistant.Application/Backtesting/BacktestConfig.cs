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

    /// <summary>Market code for cost profile selection (US, UK, UK_USD, IN).</summary>
    public string CostProfileMarket { get; init; } = "US";

    /// <summary>Base currency for the account (GBP, USD).</summary>
    public string BaseCurrency { get; init; } = "USD";

    public static BacktestConfig Default => new();
}
