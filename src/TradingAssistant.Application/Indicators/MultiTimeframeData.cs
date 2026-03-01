using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Complete multi-timeframe indicator data for a symbol.
/// Contains per-timeframe candles with indicators, plus a forward-filled daily view
/// where each daily bar includes weekly and monthly indicator values.
/// </summary>
public class MultiTimeframeData
{
    /// <summary>
    /// Per-timeframe candle arrays with their own indicators computed.
    /// </summary>
    public Dictionary<CandleInterval, CandleWithIndicators[]> TimeframeData { get; set; } = new();

    /// <summary>
    /// Daily candles with higher-timeframe indicators forward-filled.
    /// This is the primary array the backtesting engine iterates over.
    /// Weekly/Monthly indicators are accessible via CandleWithIndicators.HigherTimeframeIndicators.
    /// </summary>
    public CandleWithIndicators[] AlignedDaily { get; set; } = [];

    /// <summary>
    /// Indicator configuration used for this computation.
    /// </summary>
    public IndicatorConfig Config { get; set; } = IndicatorConfig.Default;

    /// <summary>
    /// Number of warmup bars to skip at the start of AlignedDaily for signal generation.
    /// </summary>
    public int WarmupBars { get; set; }
}
