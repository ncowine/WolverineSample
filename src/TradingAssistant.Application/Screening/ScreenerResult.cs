namespace TradingAssistant.Application.Screening;

/// <summary>
/// A single screener hit for one symbol.
/// </summary>
public class ScreenerResult
{
    public string Symbol { get; init; } = string.Empty;
    public SignalGrade Grade { get; init; }
    public decimal Score { get; init; }
    public SignalDirection Direction { get; init; }
    public decimal EntryPrice { get; init; }
    public decimal StopPrice { get; init; }
    public decimal TargetPrice { get; init; }
    public decimal RiskRewardRatio { get; init; }
    public List<GradeBreakdownEntry> Breakdown { get; init; } = new();
    public decimal? HistoricalWinRate { get; init; }
    public DateTime SignalDate { get; init; }
}

/// <summary>
/// Complete output from a screener scan.
/// </summary>
public class ScreenerRunResult
{
    public DateTime ScanDate { get; init; }
    public string StrategyName { get; init; } = string.Empty;
    public int SymbolsScanned { get; init; }
    public int SignalsFound { get; init; }
    public int SignalsPassingFilter { get; init; }

    /// <summary>
    /// Results sorted by confidence score descending, filtered by config.
    /// </summary>
    public List<ScreenerResult> Results { get; init; } = new();

    public List<string> Warnings { get; init; } = new();
    public TimeSpan ElapsedTime { get; init; }
}
