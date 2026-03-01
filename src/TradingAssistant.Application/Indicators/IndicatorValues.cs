namespace TradingAssistant.Application.Indicators;

/// <summary>
/// All computed indicator values for a single bar.
/// Zero values indicate warmup / not yet computed.
/// </summary>
public class IndicatorValues
{
    // Trend
    public decimal SmaShort { get; set; }
    public decimal SmaMedium { get; set; }
    public decimal SmaLong { get; set; }
    public decimal EmaShort { get; set; }
    public decimal EmaMedium { get; set; }
    public decimal EmaLong { get; set; }

    // Momentum
    public decimal Rsi { get; set; }
    public decimal MacdLine { get; set; }
    public decimal MacdSignal { get; set; }
    public decimal MacdHistogram { get; set; }
    public decimal StochasticK { get; set; }
    public decimal StochasticD { get; set; }

    // Volatility
    public decimal Atr { get; set; }
    public decimal BollingerUpper { get; set; }
    public decimal BollingerMiddle { get; set; }
    public decimal BollingerLower { get; set; }
    public decimal BollingerBandwidth { get; set; }
    public decimal BollingerPercentB { get; set; }

    // Volume
    public decimal Obv { get; set; }
    public decimal VolumeMa { get; set; }
    public decimal RelativeVolume { get; set; }

    /// <summary>
    /// True if this bar has passed the warmup period and has valid indicator values.
    /// </summary>
    public bool IsWarmedUp { get; set; }
}
