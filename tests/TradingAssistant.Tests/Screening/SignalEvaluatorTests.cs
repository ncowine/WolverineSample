using TradingAssistant.Application.Indicators;
using TradingAssistant.Application.Screening;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Tests.Screening;

public class SignalEvaluatorTests
{
    // ── Helpers ───────────────────────────────────────────────

    private static CandleWithIndicators MakeBar(
        decimal close = 150m,
        long volume = 1_500_000,
        decimal rsi = 55m,
        decimal smaShort = 148m,
        decimal smaMedium = 145m,
        decimal atr = 3m,
        decimal macdHist = 0.5m,
        decimal macdLine = 1m,
        decimal macdSignal = 0.5m,
        decimal stochK = 50m,
        decimal stochD = 48m,
        decimal volumeMa = 1_000_000m,
        bool warmedUp = true)
    {
        return new CandleWithIndicators
        {
            Timestamp = new DateTime(2025, 6, 15),
            Open = close - 1, High = close + 2, Low = close - 2, Close = close,
            Volume = volume,
            Interval = CandleInterval.Daily,
            Indicators = new IndicatorValues
            {
                SmaShort = smaShort, SmaMedium = smaMedium,
                Rsi = rsi, Atr = atr,
                MacdHistogram = macdHist, MacdLine = macdLine, MacdSignal = macdSignal,
                StochasticK = stochK, StochasticD = stochD,
                VolumeMa = volumeMa,
                IsWarmedUp = warmedUp
            }
        };
    }

    private static CandleWithIndicators[] MakeRecentBars(int count = 50, decimal atr = 3m, long volume = 1_000_000)
    {
        return Enumerable.Range(0, count).Select(i => new CandleWithIndicators
        {
            Timestamp = new DateTime(2025, 6, 15).AddDays(-count + i),
            Open = 148m, High = 152m, Low = 147m, Close = 150m,
            Volume = volume,
            Interval = CandleInterval.Daily,
            Indicators = new IndicatorValues { Atr = atr, IsWarmedUp = true }
        }).ToArray();
    }

    // ── Full Evaluation ──────────────────────────────────────

    [Fact]
    public void All_confirmations_pass_returns_score_1()
    {
        // Strong long signal: uptrend, RSI mid-range, high volume, normal ATR, MACD positive, Stochastic mid
        var bar = MakeBar(
            rsi: 55, smaShort: 150, smaMedium: 145,
            volume: 1_500_000, volumeMa: 1_000_000,
            atr: 3, macdHist: 0.5m, stochK: 50);
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        Assert.Equal(6, result.TotalCount);
        Assert.Equal(6, result.PassedCount);
        Assert.Equal(1.0m, result.TotalScore);
        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(SignalDirection.Long, result.Direction);
    }

    [Fact]
    public void No_confirmations_pass_returns_score_0()
    {
        // Everything opposes a long: downtrend, RSI overbought, low volume, extreme ATR, MACD negative, Stochastic overbought
        var bar = MakeBar(
            rsi: 85, smaShort: 140, smaMedium: 150,
            volume: 500_000, volumeMa: 1_000_000,
            atr: 10, macdHist: -0.5m, stochK: 90);
        var recent = MakeRecentBars(atr: 3);

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        Assert.Equal(0, result.PassedCount);
        Assert.Equal(0m, result.TotalScore);
    }

    [Fact]
    public void Partial_pass_computes_correct_score()
    {
        // 3 of 6 pass with equal weights = 0.5
        var bar = MakeBar(
            rsi: 55, smaShort: 150, smaMedium: 145, // trend: pass, momentum: pass
            volume: 500_000, volumeMa: 1_000_000,     // volume: fail
            atr: 3,                                     // volatility: pass
            macdHist: -0.3m,                           // MACD: fail
            stochK: 85);                               // stochastic: fail (overbought for long)
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        Assert.Equal(3, result.PassedCount);
        Assert.Equal(0.5m, result.TotalScore);
    }

    [Fact]
    public void Custom_weights_affect_score()
    {
        // Only trend passes, but give it weight 5, all others weight 1
        var bar = MakeBar(
            rsi: 85, smaShort: 150, smaMedium: 145, // trend: pass
            volume: 500_000, volumeMa: 1_000_000,    // volume: fail
            atr: 10,                                   // volatility: fail (extreme)
            macdHist: -0.3m,                          // MACD: fail
            stochK: 85);                              // stochastic: fail
        var recent = MakeRecentBars(atr: 3);

        var weights = new ConfirmationWeights { TrendAlignment = 5m };
        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent, weights);

        // Only trend passes (weight 5) out of total 5+1+1+1+1+1 = 10
        Assert.Equal(1, result.PassedCount);
        Assert.Equal(5m / 10m, result.TotalScore);
    }

    [Fact]
    public void Short_direction_reverses_checks()
    {
        // Downtrend, RSI oversold area, MACD negative, Stochastic low — good for short
        var bar = MakeBar(
            rsi: 35, smaShort: 140, smaMedium: 150,
            volume: 1_500_000, volumeMa: 1_000_000,
            atr: 3, macdHist: -0.5m, stochK: 25);
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Short, bar, recent);

        // Trend: SMA short < medium = pass for short
        // Momentum: RSI 35 > 30 = pass (not oversold)
        // Volume: 1.5M > 1.2 * 1M = pass
        // Volatility: ATR 3 / avg 3 = 1.0x = pass
        // MACD: histogram < 0 = pass for short
        // Stochastic: 25 > 20 = pass (not oversold)
        Assert.Equal(6, result.PassedCount);
        Assert.Equal(1.0m, result.TotalScore);
    }

    // ── Trend Alignment ──────────────────────────────────────

    [Fact]
    public void Trend_uses_weekly_indicators_when_available()
    {
        var bar = MakeBar(smaShort: 140, smaMedium: 150); // daily: downtrend
        bar.HigherTimeframeIndicators[CandleInterval.Weekly] = new IndicatorValues
        {
            SmaShort = 155, SmaMedium = 145, IsWarmedUp = true // weekly: uptrend
        };
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        var trend = result.Confirmations.Single(c => c.Name == "TrendAlignment");
        Assert.True(trend.Passed, "Should use weekly uptrend, not daily downtrend");
    }

    [Fact]
    public void Trend_falls_back_to_daily_when_no_weekly()
    {
        var bar = MakeBar(smaShort: 155, smaMedium: 145);
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        var trend = result.Confirmations.Single(c => c.Name == "TrendAlignment");
        Assert.True(trend.Passed);
    }

    [Fact]
    public void Trend_fails_when_not_warmed_up()
    {
        var bar = MakeBar(warmedUp: false);
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        var trend = result.Confirmations.Single(c => c.Name == "TrendAlignment");
        Assert.False(trend.Passed);
        Assert.Contains("Insufficient", trend.Reason);
    }

    // ── Momentum (RSI) ───────────────────────────────────────

    [Fact]
    public void Momentum_long_passes_when_rsi_below_70()
    {
        var result = SignalEvaluator.CheckMomentum(SignalDirection.Long, MakeBar(rsi: 65), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Momentum_long_fails_when_rsi_at_70()
    {
        var result = SignalEvaluator.CheckMomentum(SignalDirection.Long, MakeBar(rsi: 70), 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Momentum_short_passes_when_rsi_above_30()
    {
        var result = SignalEvaluator.CheckMomentum(SignalDirection.Short, MakeBar(rsi: 35), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Momentum_short_fails_when_rsi_at_30()
    {
        var result = SignalEvaluator.CheckMomentum(SignalDirection.Short, MakeBar(rsi: 30), 1m);
        Assert.False(result.Passed);
    }

    // ── Volume ───────────────────────────────────────────────

    [Fact]
    public void Volume_passes_at_1_2x_average()
    {
        var bar = MakeBar(volume: 1_200_000, volumeMa: 1_000_000);
        var result = SignalEvaluator.CheckVolume(bar, MakeRecentBars(), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Volume_fails_below_1_2x_average()
    {
        var bar = MakeBar(volume: 1_100_000, volumeMa: 1_000_000);
        var result = SignalEvaluator.CheckVolume(bar, MakeRecentBars(), 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Volume_uses_recent_bars_when_no_volume_ma()
    {
        var bar = MakeBar(volume: 1_500_000, volumeMa: 0);
        var recent = MakeRecentBars(volume: 1_000_000);
        var result = SignalEvaluator.CheckVolume(bar, recent, 1m);
        Assert.True(result.Passed);
    }

    // ── Volatility (ATR) ─────────────────────────────────────

    [Fact]
    public void Volatility_passes_when_atr_normal()
    {
        var bar = MakeBar(atr: 3m);
        var recent = MakeRecentBars(atr: 3m);
        var result = SignalEvaluator.CheckVolatility(bar, recent, 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Volatility_fails_when_atr_too_high()
    {
        var bar = MakeBar(atr: 7m); // 7/3 = 2.33x > 2.0x
        var recent = MakeRecentBars(atr: 3m);
        var result = SignalEvaluator.CheckVolatility(bar, recent, 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Volatility_fails_when_atr_too_low()
    {
        var bar = MakeBar(atr: 1m); // 1/3 = 0.33x < 0.5x
        var recent = MakeRecentBars(atr: 3m);
        var result = SignalEvaluator.CheckVolatility(bar, recent, 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Volatility_boundary_0_5x_passes()
    {
        var bar = MakeBar(atr: 1.5m);
        var recent = MakeRecentBars(atr: 3m);
        var result = SignalEvaluator.CheckVolatility(bar, recent, 1m);
        Assert.True(result.Passed); // 1.5/3 = 0.5x exactly
    }

    [Fact]
    public void Volatility_boundary_2_0x_passes()
    {
        var bar = MakeBar(atr: 6m);
        var recent = MakeRecentBars(atr: 3m);
        var result = SignalEvaluator.CheckVolatility(bar, recent, 1m);
        Assert.True(result.Passed); // 6/3 = 2.0x exactly
    }

    // ── MACD Histogram ───────────────────────────────────────

    [Fact]
    public void Macd_long_passes_when_positive()
    {
        var result = SignalEvaluator.CheckMacdHistogram(SignalDirection.Long, MakeBar(macdHist: 0.3m), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Macd_long_fails_when_negative()
    {
        var result = SignalEvaluator.CheckMacdHistogram(SignalDirection.Long, MakeBar(macdHist: -0.3m), 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Macd_short_passes_when_negative()
    {
        var result = SignalEvaluator.CheckMacdHistogram(SignalDirection.Short, MakeBar(macdHist: -0.3m), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Macd_not_available_fails()
    {
        var result = SignalEvaluator.CheckMacdHistogram(
            SignalDirection.Long, MakeBar(macdHist: 0, macdLine: 0, macdSignal: 0), 1m);
        Assert.False(result.Passed);
        Assert.Contains("not available", result.Reason);
    }

    // ── Stochastic ───────────────────────────────────────────

    [Fact]
    public void Stochastic_long_passes_below_80()
    {
        var result = SignalEvaluator.CheckStochastic(SignalDirection.Long, MakeBar(stochK: 60), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Stochastic_long_fails_at_80()
    {
        var result = SignalEvaluator.CheckStochastic(SignalDirection.Long, MakeBar(stochK: 80), 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Stochastic_short_passes_above_20()
    {
        var result = SignalEvaluator.CheckStochastic(SignalDirection.Short, MakeBar(stochK: 40), 1m);
        Assert.True(result.Passed);
    }

    [Fact]
    public void Stochastic_short_fails_at_20()
    {
        var result = SignalEvaluator.CheckStochastic(SignalDirection.Short, MakeBar(stochK: 20), 1m);
        Assert.False(result.Passed);
    }

    [Fact]
    public void Stochastic_not_available_fails()
    {
        var result = SignalEvaluator.CheckStochastic(
            SignalDirection.Long, MakeBar(stochK: 0, stochD: 0), 1m);
        Assert.False(result.Passed);
    }

    // ── MultiTimeframeData Overload ──────────────────────────

    [Fact]
    public void Evaluate_with_mtf_data()
    {
        var bars = new CandleWithIndicators[60];
        for (var i = 0; i < 60; i++)
        {
            bars[i] = MakeBar(
                close: 150m + i * 0.1m,
                volume: 1_500_000,
                rsi: 55, smaShort: 150, smaMedium: 145,
                atr: 3, macdHist: 0.5m, stochK: 50,
                volumeMa: 1_000_000);
            bars[i].Timestamp = new DateTime(2025, 4, 1).AddDays(i);
        }

        var mtf = new MultiTimeframeData
        {
            AlignedDaily = bars,
            WarmupBars = 0
        };

        var result = SignalEvaluator.Evaluate("AAPL", 55, SignalDirection.Long, mtf);

        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(6, result.TotalCount);
        Assert.True(result.TotalScore > 0);
    }

    [Fact]
    public void Evaluate_with_mtf_throws_for_out_of_range()
    {
        var mtf = new MultiTimeframeData { AlignedDaily = new CandleWithIndicators[10] };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SignalEvaluator.Evaluate("AAPL", 15, SignalDirection.Long, mtf));
    }

    // ── Each confirmation has a reason ───────────────────────

    [Fact]
    public void All_confirmations_have_reasons()
    {
        var bar = MakeBar();
        var recent = MakeRecentBars();

        var result = SignalEvaluator.Evaluate("AAPL", bar.Timestamp, SignalDirection.Long, bar, recent);

        Assert.All(result.Confirmations, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.Name));
            Assert.False(string.IsNullOrEmpty(c.Reason));
            Assert.True(c.Weight > 0);
        });
    }
}
