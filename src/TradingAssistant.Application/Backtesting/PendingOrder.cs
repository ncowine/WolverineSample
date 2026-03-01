namespace TradingAssistant.Application.Backtesting;

public enum OrderType { Market, Limit }
public enum OrderSide { Buy, Sell }

/// <summary>
/// An order placed on bar N, to be filled on bar N+1 (no look-ahead bias).
/// </summary>
public class PendingOrder
{
    public string Symbol { get; init; } = string.Empty;
    public OrderType Type { get; init; }
    public OrderSide Side { get; init; }
    public int Shares { get; init; }
    public decimal? LimitPrice { get; init; }
    public decimal StopLoss { get; init; }
    public decimal TakeProfit { get; init; }
    public DateTime SignalDate { get; init; }
}
