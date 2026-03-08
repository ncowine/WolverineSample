namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Complete output from the unified backtest engine (single-symbol or portfolio).
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

    // Portfolio-specific fields
    public int UniqueSymbolsTraded { get; init; }
    public decimal AveragePositionsHeld { get; init; }
    public int MaxPositionsHeld { get; init; }
    public Dictionary<string, SymbolBreakdown> SymbolBreakdowns { get; init; } = new();
    public List<(DateTime Date, string Regime)> RegimeTimeline { get; init; } = new();

    // Computed metrics
    public int TotalTrades => Trades.Count;
    public int WinningTrades => Trades.Count(t => t.PnL > 0);
    public int LosingTrades => Trades.Count(t => t.PnL <= 0);
    public decimal WinRate => TotalTrades == 0 ? 0 : (decimal)WinningTrades / TotalTrades * 100;
    public decimal TotalReturn => InitialCapital == 0 ? 0 : (FinalEquity - InitialCapital) / InitialCapital * 100;
    public decimal TotalPnL => Trades.Sum(t => t.PnL);
    public decimal TotalCommissions => Trades.Sum(t => t.Commission);
    public decimal GrossPnL => TotalPnL + TotalCommissions;
    public decimal GrossReturn => InitialCapital == 0 ? 0 : GrossPnL / InitialCapital * 100;
    public decimal NetReturn => TotalReturn;
    public decimal CostDrag => GrossReturn - NetReturn;
}

public class SymbolBreakdown
{
    public string Symbol { get; init; } = string.Empty;
    public int Trades { get; init; }
    public int Wins { get; init; }
    public decimal WinRate { get; init; }
    public decimal TotalPnL { get; init; }
    public decimal AvgPnLPercent { get; init; }
    public decimal AvgHoldingDays { get; init; }
}
