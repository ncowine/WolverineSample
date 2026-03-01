using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.MarketData;

/// <summary>
/// A persisted screener scan run with its results.
/// </summary>
public class ScreenerRun : BaseEntity
{
    public DateTime ScanDate { get; set; }
    public Guid? StrategyId { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public int SymbolsScanned { get; set; }
    public int SignalsFound { get; set; }

    /// <summary>
    /// JSON-serialized list of ScreenerResult entries.
    /// </summary>
    public string ResultsJson { get; set; } = "[]";

    /// <summary>
    /// JSON-serialized warnings from the scan.
    /// </summary>
    public string WarningsJson { get; set; } = "[]";

    public TimeSpan ElapsedTime { get; set; }
}
