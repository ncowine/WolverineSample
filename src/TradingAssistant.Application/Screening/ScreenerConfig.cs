namespace TradingAssistant.Application.Screening;

/// <summary>
/// Configuration for a screener scan.
/// </summary>
public class ScreenerConfig
{
    /// <summary>
    /// Minimum grade to include in results (default: B — only A and B pass).
    /// </summary>
    public SignalGrade MinGrade { get; init; } = SignalGrade.B;

    /// <summary>
    /// Minimum daily volume to consider a symbol.
    /// </summary>
    public long? MinVolume { get; init; }

    /// <summary>
    /// Optional sector filter (only scan these sectors).
    /// </summary>
    public List<string>? Sectors { get; init; }

    /// <summary>
    /// Maximum number of signals to return (default 20).
    /// </summary>
    public int MaxSignals { get; init; } = 20;

    /// <summary>
    /// Historical win rate to use when no per-symbol history is available.
    /// If null, the grader uses a neutral 50%.
    /// </summary>
    public decimal? DefaultWinRate { get; init; }
}
