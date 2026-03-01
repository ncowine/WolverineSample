namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Complete output from a backtest run.
/// </summary>
public class BacktestEngineResult
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal InitialCapital { get; init; }
    public decimal FinalEquity { get; init; }

    public List<TradeRecord> Trades { get; init; } = new();
    public List<EquityPoint> EquityCurve { get; init; } = new();
    public List<string> Log { get; init; } = new();

    public int TotalTrades => Trades.Count;
    public int WinningTrades => Trades.Count(t => t.PnL > 0);
    public int LosingTrades => Trades.Count(t => t.PnL <= 0);
    public decimal WinRate => TotalTrades == 0 ? 0 : (decimal)WinningTrades / TotalTrades * 100;
    public decimal TotalReturn => InitialCapital == 0 ? 0 : (FinalEquity - InitialCapital) / InitialCapital * 100;
    public decimal TotalPnL => Trades.Sum(t => t.PnL);
    public decimal TotalCommissions => Trades.Sum(t => t.Commission);
}
