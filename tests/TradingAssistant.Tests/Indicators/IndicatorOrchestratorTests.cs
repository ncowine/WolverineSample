using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;

namespace TradingAssistant.Tests.Indicators;

public class IndicatorOrchestratorTests
{
    private readonly IndicatorConfig _config = new()
    {
        SmaShortPeriod = 3,
        SmaMediumPeriod = 5,
        SmaLongPeriod = 10,
        EmaShortPeriod = 3,
        EmaMediumPeriod = 5,
        EmaLongPeriod = 10,
        RsiPeriod = 5,
        MacdFastPeriod = 3,
        MacdSlowPeriod = 5,
        MacdSignalPeriod = 3,
        StochasticKPeriod = 5,
        StochasticDPeriod = 3,
        AtrPeriod = 5,
        BollingerPeriod = 5,
        BollingerMultiplier = 2m,
        VolumeMaPeriod = 5
    };

    [Fact]
    public void Computes_all_indicators_for_daily_timeframe()
    {
        var candles = GenerateCandles(30, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        Assert.True(result.TimeframeData.ContainsKey(CandleInterval.Daily));
        Assert.Equal(30, result.TimeframeData[CandleInterval.Daily].Length);
    }

    [Fact]
    public void Trend_indicators_populated()
    {
        var candles = GenerateCandles(30, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);
        var bars = result.TimeframeData[CandleInterval.Daily];

        // After warmup, SMA/EMA should be non-zero
        var lastBar = bars[^1];
        Assert.NotEqual(0m, lastBar.Indicators.SmaShort);
        Assert.NotEqual(0m, lastBar.Indicators.SmaMedium);
        Assert.NotEqual(0m, lastBar.Indicators.SmaLong);
        Assert.NotEqual(0m, lastBar.Indicators.EmaShort);
        Assert.NotEqual(0m, lastBar.Indicators.EmaMedium);
        Assert.NotEqual(0m, lastBar.Indicators.EmaLong);
    }

    [Fact]
    public void Momentum_indicators_populated()
    {
        var candles = GenerateCandles(30, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);
        var lastBar = result.TimeframeData[CandleInterval.Daily][^1];

        Assert.NotEqual(0m, lastBar.Indicators.Rsi);
        Assert.InRange(lastBar.Indicators.Rsi, 0m, 100m);
        // MACD components should have values after warmup
        Assert.NotEqual(0m, lastBar.Indicators.MacdLine);
        Assert.NotEqual(0m, lastBar.Indicators.StochasticK);
    }

    [Fact]
    public void Volatility_indicators_populated()
    {
        var candles = GenerateCandles(30, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);
        var lastBar = result.TimeframeData[CandleInterval.Daily][^1];

        Assert.True(lastBar.Indicators.Atr > 0);
        Assert.True(lastBar.Indicators.BollingerUpper > lastBar.Indicators.BollingerMiddle);
        Assert.True(lastBar.Indicators.BollingerMiddle > lastBar.Indicators.BollingerLower);
    }

    [Fact]
    public void Volume_indicators_populated()
    {
        var candles = GenerateCandles(30, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);
        var lastBar = result.TimeframeData[CandleInterval.Daily][^1];

        Assert.NotEqual(0m, lastBar.Indicators.Obv);
        Assert.NotEqual(0m, lastBar.Indicators.VolumeMa);
        Assert.NotEqual(0m, lastBar.Indicators.RelativeVolume);
    }

    [Fact]
    public void Warmup_flag_set_correctly()
    {
        var candles = GenerateCandles(30, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);
        var bars = result.TimeframeData[CandleInterval.Daily];

        // First few bars should not be warmed up
        Assert.False(bars[0].Indicators.IsWarmedUp);

        // Last bar should be warmed up (30 bars, config.MaxWarmupBars is ~10)
        Assert.True(bars[^1].Indicators.IsWarmedUp);
    }

    [Fact]
    public void AlignedDaily_populated_from_daily_candles()
    {
        var candles = GenerateCandles(20, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        Assert.Equal(20, result.AlignedDaily.Length);
        Assert.Equal(candles[0].Timestamp, result.AlignedDaily[0].Timestamp);
    }

    [Fact]
    public void Forward_fills_weekly_indicators_onto_daily()
    {
        // Create daily candles (Mon-Fri for 3 weeks = 15 days)
        var daily = new List<PriceCandle>();
        var baseDate = new DateTime(2025, 1, 6); // Monday
        for (var i = 0; i < 15; i++)
        {
            var date = baseDate.AddDays(i);
            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;
            daily.Add(MakeCandle(date, 100m + i, CandleInterval.Daily));
        }

        // Create weekly candles (one per week start)
        var weekly = new List<PriceCandle>
        {
            MakeCandle(new DateTime(2025, 1, 6), 102m, CandleInterval.Weekly),  // Week 1
            MakeCandle(new DateTime(2025, 1, 13), 107m, CandleInterval.Weekly), // Week 2
        };

        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, daily },
            { CandleInterval.Weekly, weekly }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        // Daily bars during week 2 (Jan 13+) should have weekly indicators from week 1 or 2
        var jan14Bar = result.AlignedDaily.FirstOrDefault(b => b.Timestamp.Date == new DateTime(2025, 1, 14));
        Assert.NotNull(jan14Bar);
        Assert.True(jan14Bar.HigherTimeframeIndicators.ContainsKey(CandleInterval.Weekly));
    }

    [Fact]
    public void Forward_fills_monthly_indicators_onto_daily()
    {
        var daily = GenerateCandles(40, CandleInterval.Daily, new DateTime(2025, 1, 1));
        var monthly = new List<PriceCandle>
        {
            MakeCandle(new DateTime(2025, 1, 1), 105m, CandleInterval.Monthly),
        };

        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, daily },
            { CandleInterval.Monthly, monthly }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        // Daily bars after Jan 1 should have monthly indicators forward-filled
        var laterBar = result.AlignedDaily[^1];
        Assert.True(laterBar.HigherTimeframeIndicators.ContainsKey(CandleInterval.Monthly));
    }

    [Fact]
    public void Multiple_timeframes_computed_independently()
    {
        var daily = GenerateCandles(30, CandleInterval.Daily);
        var weekly = GenerateCandles(10, CandleInterval.Weekly);

        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, daily },
            { CandleInterval.Weekly, weekly }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        Assert.True(result.TimeframeData.ContainsKey(CandleInterval.Daily));
        Assert.True(result.TimeframeData.ContainsKey(CandleInterval.Weekly));
        Assert.Equal(30, result.TimeframeData[CandleInterval.Daily].Length);
        Assert.Equal(10, result.TimeframeData[CandleInterval.Weekly].Length);
    }

    [Fact]
    public void Empty_candles_produces_empty_result()
    {
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, new List<PriceCandle>() }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        Assert.Empty(result.AlignedDaily);
    }

    [Fact]
    public void Config_stored_in_result()
    {
        var candles = GenerateCandles(10, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);

        Assert.Same(_config, result.Config);
        Assert.Equal(_config.MaxWarmupBars, result.WarmupBars);
    }

    [Fact]
    public void Default_config_used_when_null()
    {
        var candles = GenerateCandles(60, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, config: null);

        Assert.Equal(IndicatorConfig.Default.RsiPeriod, result.Config.RsiPeriod);
    }

    [Fact]
    public void Candle_ohlcv_preserved_in_output()
    {
        var candles = GenerateCandles(10, CandleInterval.Daily);
        var data = new Dictionary<CandleInterval, List<PriceCandle>>
        {
            { CandleInterval.Daily, candles }
        };

        var result = IndicatorOrchestrator.Compute(data, _config);
        var bars = result.TimeframeData[CandleInterval.Daily];

        for (var i = 0; i < candles.Count; i++)
        {
            Assert.Equal(candles[i].Open, bars[i].Open);
            Assert.Equal(candles[i].High, bars[i].High);
            Assert.Equal(candles[i].Low, bars[i].Low);
            Assert.Equal(candles[i].Close, bars[i].Close);
            Assert.Equal(candles[i].Volume, bars[i].Volume);
            Assert.Equal(candles[i].Timestamp, bars[i].Timestamp);
        }
    }

    // --- Helpers ---

    private static List<PriceCandle> GenerateCandles(int count, CandleInterval interval, DateTime? startDate = null)
    {
        var rng = new Random(42); // deterministic
        var start = startDate ?? new DateTime(2025, 1, 2);
        var candles = new List<PriceCandle>();
        var price = 100m;

        for (var i = 0; i < count; i++)
        {
            var change = (decimal)(rng.NextDouble() * 4 - 2); // -2 to +2
            price = Math.Max(50, price + change);
            var high = price + (decimal)(rng.NextDouble() * 2);
            var low = price - (decimal)(rng.NextDouble() * 2);

            candles.Add(new PriceCandle
            {
                StockId = Guid.NewGuid(),
                Open = price - change / 2,
                High = high,
                Low = low,
                Close = price,
                Volume = 1_000_000 + rng.Next(500_000),
                Timestamp = start.AddDays(i),
                Interval = interval
            });
        }

        return candles;
    }

    private static PriceCandle MakeCandle(DateTime date, decimal close, CandleInterval interval)
    {
        return new PriceCandle
        {
            StockId = Guid.NewGuid(),
            Open = close - 1,
            High = close + 2,
            Low = close - 2,
            Close = close,
            Volume = 1_000_000,
            Timestamp = date,
            Interval = interval
        };
    }
}
