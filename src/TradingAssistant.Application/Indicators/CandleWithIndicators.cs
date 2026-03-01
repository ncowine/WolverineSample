using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Application.Indicators;

/// <summary>
/// A price candle combined with all computed indicator values.
/// This is the primary data structure the backtesting engine iterates over.
/// </summary>
public class CandleWithIndicators
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public CandleInterval Interval { get; set; }

    public IndicatorValues Indicators { get; set; } = new();

    /// <summary>
    /// Forward-filled indicators from higher timeframes.
    /// Key = timeframe (Weekly, Monthly). Only populated on Daily bars.
    /// </summary>
    public Dictionary<CandleInterval, IndicatorValues> HigherTimeframeIndicators { get; set; } = new();
}
