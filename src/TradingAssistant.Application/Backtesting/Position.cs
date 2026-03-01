namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// An open position held by the backtest engine.
/// </summary>
public class Position
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime EntryDate { get; init; }
    public decimal EntryPrice { get; init; }
    public int Shares { get; init; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }

    /// <summary>
    /// Total cost basis including slippage and commission.
    /// </summary>
    public decimal CostBasis { get; init; }

    /// <summary>
    /// Risk in dollars: shares * (entry - stop).
    /// </summary>
    public decimal RiskAmount => Shares * (EntryPrice - StopLoss);

    public decimal UnrealizedPnL(decimal currentPrice) =>
        Shares * (currentPrice - EntryPrice);

    public decimal MarketValue(decimal currentPrice) =>
        Shares * currentPrice;
}
