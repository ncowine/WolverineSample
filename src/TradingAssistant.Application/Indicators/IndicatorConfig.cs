namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Configurable parameters for all indicator calculations.
/// Sensible defaults provided — override as needed for optimization.
/// </summary>
public class IndicatorConfig
{
    // Trend indicators
    public int SmaShortPeriod { get; init; } = 10;
    public int SmaMediumPeriod { get; init; } = 20;
    public int SmaLongPeriod { get; init; } = 50;
    public int EmaShortPeriod { get; init; } = 12;
    public int EmaMediumPeriod { get; init; } = 26;
    public int EmaLongPeriod { get; init; } = 50;

    // Momentum
    public int RsiPeriod { get; init; } = 14;
    public int MacdFastPeriod { get; init; } = 12;
    public int MacdSlowPeriod { get; init; } = 26;
    public int MacdSignalPeriod { get; init; } = 9;
    public int StochasticKPeriod { get; init; } = 14;
    public int StochasticDPeriod { get; init; } = 3;

    // Volatility
    public int AtrPeriod { get; init; } = 14;
    public int BollingerPeriod { get; init; } = 20;
    public decimal BollingerMultiplier { get; init; } = 2m;

    // Volume
    public int VolumeMaPeriod { get; init; } = 20;

    /// <summary>
    /// Maximum warmup bars needed across all indicators.
    /// Bars before this index should not be used for signal generation.
    /// </summary>
    public int MaxWarmupBars =>
        Math.Max(
            Math.Max(SmaLongPeriod, EmaLongPeriod),
            Math.Max(
                MacdSlowPeriod + MacdSignalPeriod,
                Math.Max(BollingerPeriod, StochasticKPeriod + StochasticDPeriod)));

    public static IndicatorConfig Default => new();
}
