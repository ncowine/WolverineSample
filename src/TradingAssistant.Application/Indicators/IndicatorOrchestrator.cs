using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Application.Indicators;

/// <summary>
/// Computes all indicators on all timeframes and produces aligned multi-timeframe data.
/// The resulting AlignedDaily array has weekly/monthly indicators forward-filled onto daily bars.
/// </summary>
public static class IndicatorOrchestrator
{
    /// <summary>
    /// Compute indicators for all provided timeframes and produce aligned daily data.
    /// </summary>
    /// <param name="candlesByTimeframe">Candles grouped by interval (Daily, Weekly, Monthly). Must be sorted by timestamp.</param>
    /// <param name="config">Indicator calculation parameters.</param>
    public static MultiTimeframeData Compute(
        Dictionary<CandleInterval, List<PriceCandle>> candlesByTimeframe,
        IndicatorConfig? config = null)
    {
        config ??= IndicatorConfig.Default;

        var result = new MultiTimeframeData
        {
            Config = config,
            WarmupBars = config.MaxWarmupBars
        };

        // Compute indicators per timeframe
        foreach (var (interval, candles) in candlesByTimeframe)
        {
            if (candles.Count == 0) continue;
            var withIndicators = ComputeForTimeframe(candles, interval, config);
            result.TimeframeData[interval] = withIndicators;
        }

        // Build forward-filled daily view
        if (result.TimeframeData.TryGetValue(CandleInterval.Daily, out var dailyBars) && dailyBars.Length > 0)
        {
            result.AlignedDaily = ForwardFillHigherTimeframes(dailyBars, result.TimeframeData, config);
        }

        return result;
    }

    /// <summary>
    /// Compute all indicators for a single timeframe's candle array.
    /// </summary>
    internal static CandleWithIndicators[] ComputeForTimeframe(
        List<PriceCandle> candles, CandleInterval interval, IndicatorConfig config)
    {
        var count = candles.Count;
        var closes = new decimal[count];
        var highs = new decimal[count];
        var lows = new decimal[count];
        var volumes = new long[count];

        for (var i = 0; i < count; i++)
        {
            closes[i] = candles[i].Close;
            highs[i] = candles[i].High;
            lows[i] = candles[i].Low;
            volumes[i] = candles[i].Volume;
        }

        // --- Trend ---
        var smaShort = SmaCalculator.Instance.Calculate(closes, config.SmaShortPeriod);
        var smaMedium = SmaCalculator.Instance.Calculate(closes, config.SmaMediumPeriod);
        var smaLong = SmaCalculator.Instance.Calculate(closes, config.SmaLongPeriod);
        var emaShort = EmaCalculator.Instance.Calculate(closes, config.EmaShortPeriod);
        var emaMedium = EmaCalculator.Instance.Calculate(closes, config.EmaMediumPeriod);
        var emaLong = EmaCalculator.Instance.Calculate(closes, config.EmaLongPeriod);

        // --- Momentum ---
        var rsi = RsiCalculator.Instance.Calculate(closes, config.RsiPeriod);
        var macd = MacdCalculator.Instance.Calculate(closes, config.MacdFastPeriod, config.MacdSlowPeriod, config.MacdSignalPeriod);
        var stoch = StochasticCalculator.Instance.Calculate(highs, lows, closes, config.StochasticKPeriod, config.StochasticDPeriod);

        // --- Volatility ---
        var atr = AtrCalculator.Instance.Calculate(highs, lows, closes, config.AtrPeriod);
        var bb = BollingerBandsCalculator.Instance.Calculate(closes, config.BollingerPeriod, config.BollingerMultiplier);

        // --- Volume ---
        var obv = ObvCalculator.Instance.Calculate(closes, volumes);
        var volProfile = VolumeProfileCalculator.Instance.Calculate(volumes, config.VolumeMaPeriod);

        // Assemble results
        var result = new CandleWithIndicators[count];
        for (var i = 0; i < count; i++)
        {
            var isWarmedUp = i >= config.MaxWarmupBars;

            result[i] = new CandleWithIndicators
            {
                Timestamp = candles[i].Timestamp,
                Open = candles[i].Open,
                High = highs[i],
                Low = lows[i],
                Close = closes[i],
                Volume = volumes[i],
                Interval = interval,
                Indicators = new IndicatorValues
                {
                    SmaShort = smaShort[i],
                    SmaMedium = smaMedium[i],
                    SmaLong = smaLong[i],
                    EmaShort = emaShort[i],
                    EmaMedium = emaMedium[i],
                    EmaLong = emaLong[i],
                    Rsi = rsi[i],
                    MacdLine = macd.Macd[i],
                    MacdSignal = macd.Signal[i],
                    MacdHistogram = macd.Histogram[i],
                    StochasticK = stoch.K[i],
                    StochasticD = stoch.D[i],
                    Atr = atr[i],
                    BollingerUpper = bb.Upper[i],
                    BollingerMiddle = bb.Middle[i],
                    BollingerLower = bb.Lower[i],
                    BollingerBandwidth = bb.Bandwidth[i],
                    BollingerPercentB = bb.PercentB[i],
                    Obv = obv[i],
                    VolumeMa = volProfile.VolumeMa[i],
                    RelativeVolume = volProfile.RelativeVolume[i],
                    IsWarmedUp = isWarmedUp
                }
            };
        }

        return result;
    }

    /// <summary>
    /// Forward-fill weekly and monthly indicators onto daily bars.
    /// Monday's daily bar uses the previous completed week's weekly indicators.
    /// Each daily bar uses the previous completed month's monthly indicators.
    /// </summary>
    internal static CandleWithIndicators[] ForwardFillHigherTimeframes(
        CandleWithIndicators[] dailyBars,
        Dictionary<CandleInterval, CandleWithIndicators[]> timeframeData,
        IndicatorConfig config)
    {
        // Build lookup for weekly and monthly by timestamp for fast retrieval
        var weeklyLookup = BuildTimeframeLookup(timeframeData, CandleInterval.Weekly);
        var monthlyLookup = BuildTimeframeLookup(timeframeData, CandleInterval.Monthly);

        IndicatorValues? lastWeekly = null;
        IndicatorValues? lastMonthly = null;

        foreach (var daily in dailyBars)
        {
            // Find the most recent weekly indicator on or before this daily date
            lastWeekly = FindMostRecent(weeklyLookup, daily.Timestamp) ?? lastWeekly;
            lastMonthly = FindMostRecent(monthlyLookup, daily.Timestamp) ?? lastMonthly;

            if (lastWeekly != null)
                daily.HigherTimeframeIndicators[CandleInterval.Weekly] = lastWeekly;

            if (lastMonthly != null)
                daily.HigherTimeframeIndicators[CandleInterval.Monthly] = lastMonthly;
        }

        return dailyBars;
    }

    private static SortedList<DateTime, IndicatorValues> BuildTimeframeLookup(
        Dictionary<CandleInterval, CandleWithIndicators[]> data, CandleInterval interval)
    {
        var lookup = new SortedList<DateTime, IndicatorValues>();
        if (!data.TryGetValue(interval, out var bars))
            return lookup;

        foreach (var bar in bars)
        {
            // Use TryAdd to handle potential duplicate timestamps
            lookup.TryAdd(bar.Timestamp.Date, bar.Indicators);
        }

        return lookup;
    }

    /// <summary>
    /// Find the most recent entry in the sorted list on or before the given date.
    /// Returns null if no such entry exists.
    /// </summary>
    private static IndicatorValues? FindMostRecent(SortedList<DateTime, IndicatorValues> lookup, DateTime date)
    {
        if (lookup.Count == 0)
            return null;

        var targetDate = date.Date;

        // Binary search approach using Keys
        var keys = lookup.Keys;

        // If the exact date exists, return it
        if (lookup.TryGetValue(targetDate, out var exact))
            return exact;

        // Find the largest key <= targetDate
        var lo = 0;
        var hi = keys.Count - 1;
        var bestIndex = -1;

        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (keys[mid] <= targetDate)
            {
                bestIndex = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return bestIndex >= 0 ? lookup.Values[bestIndex] : null;
    }
}
