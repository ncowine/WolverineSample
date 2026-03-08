using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Backtesting;

public class BacktestRun : BaseEntity
{
    public Guid StrategyId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public BacktestRunStatus Status { get; set; } = BacktestRunStatus.Pending;

    // Portfolio/universe fields (null for single-symbol backtests)
    public Guid? UniverseId { get; set; }
    public string? UniverseName { get; set; }
    public decimal InitialCapital { get; set; } = 100_000m;
    public int MaxPositions { get; set; } = 6;
    public int? TotalSymbols { get; set; }
    public int? SymbolsWithData { get; set; }

    /// <summary>True when this is a universe/portfolio backtest.</summary>
    public bool IsPortfolio => UniverseId.HasValue;

    public Strategy Strategy { get; set; } = null!;
    public BacktestResult? Result { get; set; }
}
