namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// A completed trade (entry + exit).
/// </summary>
public record TradeRecord
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime EntryDate { get; init; }
    public decimal EntryPrice { get; init; }
    public DateTime ExitDate { get; init; }
    public decimal ExitPrice { get; init; }
    public int Shares { get; init; }
    public decimal PnL { get; init; }
    public decimal PnLPercent { get; init; }
    public decimal Commission { get; init; }
    public int HoldingDays { get; init; }
    public string ExitReason { get; init; } = string.Empty;
    public decimal StopLossPrice { get; init; }
    public decimal InitialStopLoss { get; init; }
    public decimal TakeProfitPrice { get; init; }

    // Trade reasoning (serialized as part of TradeLogJson, no schema migration needed)
    public string? ReasoningJson { get; init; }
    public decimal SignalScore { get; init; }
    public string? Regime { get; init; }
}
