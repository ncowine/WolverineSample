using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Application.Screening;

/// <summary>
/// Evaluates a trade signal against multiple independent confirmations.
/// Strategy-independent: applies to ANY signal as a quality filter.
/// </summary>
public static class SignalEvaluator
{
    /// <summary>
    /// Default confirmation weights. Can be overridden via <see cref="ConfirmationWeights"/>.
    /// </summary>
    public static readonly ConfirmationWeights DefaultWeights = new();

    /// <summary>
    /// Evaluate a trade signal against all confirmations.
    /// </summary>
    /// <param name="symbol">Ticker symbol.</param>
    /// <param name="date">Signal date.</param>
    /// <param name="direction">Long or Short.</param>
    /// <param name="bar">The signal bar (daily, with indicators computed).</param>
    /// <param name="recentBars">Recent bars for computing averages (at least 50 for ATR average). Most recent last.</param>
    /// <param name="weights">Optional weight overrides.</param>
    public static SignalEvaluation Evaluate(
        string symbol,
        DateTime date,
        SignalDirection direction,
        CandleWithIndicators bar,
        CandleWithIndicators[] recentBars,
        ConfirmationWeights? weights = null)
    {
        weights ??= DefaultWeights;

        var confirmations = new List<ConfirmationResult>
        {
            CheckTrendAlignment(direction, bar, weights.TrendAlignment),
            CheckMomentum(direction, bar, weights.Momentum),
            CheckVolume(bar, recentBars, weights.Volume),
            CheckVolatility(bar, recentBars, weights.Volatility),
            CheckMacdHistogram(direction, bar, weights.MacdHistogram),
            CheckStochastic(direction, bar, weights.Stochastic)
        };

        var totalWeight = confirmations.Sum(c => c.Weight);
        var passedWeight = confirmations.Where(c => c.Passed).Sum(c => c.Weight);
        var score = totalWeight > 0 ? passedWeight / totalWeight : 0m;

        return new SignalEvaluation
        {
            Symbol = symbol,
            Date = date,
            Direction = direction,
            Confirmations = confirmations,
            TotalScore = score
        };
    }

    /// <summary>
    /// Overload that accepts MultiTimeframeData and a bar index.
    /// Uses higher-timeframe indicators from the daily bar if available.
    /// </summary>
    public static SignalEvaluation Evaluate(
        string symbol,
        int barIndex,
        SignalDirection direction,
        MultiTimeframeData mtfData,
        ConfirmationWeights? weights = null)
    {
        var daily = mtfData.AlignedDaily;
        if (barIndex < 0 || barIndex >= daily.Length)
            throw new ArgumentOutOfRangeException(nameof(barIndex));

        var bar = daily[barIndex];

        // Grab up to 50 recent bars for averages
        var lookback = Math.Min(barIndex + 1, 50);
        var recentBars = daily[(barIndex - lookback + 1)..(barIndex + 1)];

        return Evaluate(symbol, bar.Timestamp, direction, bar, recentBars, weights);
    }

    // ── Individual Confirmations ──────────────────────────────

    /// <summary>
    /// Trend alignment: higher-timeframe trend direction matches entry direction.
    /// Uses weekly SMA short vs medium (if available), otherwise daily SMA short vs long.
    /// Long: SMA short > SMA medium/long. Short: SMA short &lt; SMA medium/long.
    /// </summary>
    internal static ConfirmationResult CheckTrendAlignment(
        SignalDirection direction, CandleWithIndicators bar, decimal weight)
    {
        // Prefer weekly timeframe indicators if available
        var indicators = bar.HigherTimeframeIndicators.TryGetValue(CandleInterval.Weekly, out var weekly)
            && weekly.IsWarmedUp
            ? weekly
            : bar.Indicators;

        if (!indicators.IsWarmedUp || indicators.SmaShort == 0 || indicators.SmaMedium == 0)
        {
            return new ConfirmationResult
            {
                Name = "TrendAlignment",
                Passed = false,
                Weight = weight,
                Reason = "Insufficient data for trend alignment"
            };
        }

        var trendUp = indicators.SmaShort > indicators.SmaMedium;
        var passed = direction == SignalDirection.Long ? trendUp : !trendUp;

        return new ConfirmationResult
        {
            Name = "TrendAlignment",
            Passed = passed,
            Weight = weight,
            Reason = passed
                ? $"Trend aligns with {direction} (SMA short {indicators.SmaShort:F2} vs medium {indicators.SmaMedium:F2})"
                : $"Trend opposes {direction} (SMA short {indicators.SmaShort:F2} vs medium {indicators.SmaMedium:F2})"
        };
    }

    /// <summary>
    /// Momentum confirmation: RSI not overbought/oversold against the trade.
    /// Long: RSI &lt; 70 (not overbought). Short: RSI &gt; 30 (not oversold).
    /// </summary>
    internal static ConfirmationResult CheckMomentum(
        SignalDirection direction, CandleWithIndicators bar, decimal weight)
    {
        var rsi = bar.Indicators.Rsi;
        if (rsi == 0)
        {
            return new ConfirmationResult
            {
                Name = "Momentum",
                Passed = false,
                Weight = weight,
                Reason = "RSI not available"
            };
        }

        bool passed;
        string reason;

        if (direction == SignalDirection.Long)
        {
            passed = rsi < 70m;
            reason = passed
                ? $"RSI {rsi:F1} not overbought (< 70)"
                : $"RSI {rsi:F1} overbought (>= 70), risky for long entry";
        }
        else
        {
            passed = rsi > 30m;
            reason = passed
                ? $"RSI {rsi:F1} not oversold (> 30)"
                : $"RSI {rsi:F1} oversold (<= 30), risky for short entry";
        }

        return new ConfirmationResult
        {
            Name = "Momentum",
            Passed = passed,
            Weight = weight,
            Reason = reason
        };
    }

    /// <summary>
    /// Volume confirmation: signal bar volume > 1.2x average volume.
    /// Uses VolumeMa if available, otherwise computes from recent bars.
    /// </summary>
    internal static ConfirmationResult CheckVolume(
        CandleWithIndicators bar, CandleWithIndicators[] recentBars, decimal weight)
    {
        decimal avgVolume;
        if (bar.Indicators.VolumeMa > 0)
        {
            avgVolume = bar.Indicators.VolumeMa;
        }
        else if (recentBars.Length > 1)
        {
            avgVolume = (decimal)recentBars.Average(b => b.Volume);
        }
        else
        {
            return new ConfirmationResult
            {
                Name = "Volume",
                Passed = false,
                Weight = weight,
                Reason = "Insufficient data for volume average"
            };
        }

        if (avgVolume <= 0)
        {
            return new ConfirmationResult
            {
                Name = "Volume",
                Passed = false,
                Weight = weight,
                Reason = "Average volume is zero"
            };
        }

        var ratio = bar.Volume / avgVolume;
        var passed = ratio >= 1.2m;

        return new ConfirmationResult
        {
            Name = "Volume",
            Passed = passed,
            Weight = weight,
            Reason = passed
                ? $"Volume {bar.Volume:N0} is {ratio:F2}x average ({avgVolume:N0})"
                : $"Volume {bar.Volume:N0} is only {ratio:F2}x average ({avgVolume:N0}), below 1.2x threshold"
        };
    }

    /// <summary>
    /// Volatility check: ATR within 0.5x to 2.0x of the 50-day ATR average.
    /// Ensures neither too calm (false breakout risk) nor too volatile (stop risk).
    /// </summary>
    internal static ConfirmationResult CheckVolatility(
        CandleWithIndicators bar, CandleWithIndicators[] recentBars, decimal weight)
    {
        var currentAtr = bar.Indicators.Atr;
        if (currentAtr <= 0)
        {
            return new ConfirmationResult
            {
                Name = "Volatility",
                Passed = false,
                Weight = weight,
                Reason = "ATR not available"
            };
        }

        // Compute average ATR from recent bars
        var barsWithAtr = recentBars.Where(b => b.Indicators.Atr > 0).ToArray();
        if (barsWithAtr.Length < 5)
        {
            return new ConfirmationResult
            {
                Name = "Volatility",
                Passed = false,
                Weight = weight,
                Reason = "Insufficient ATR history for average"
            };
        }

        var avgAtr = barsWithAtr.Average(b => b.Indicators.Atr);
        var ratio = avgAtr > 0 ? currentAtr / avgAtr : 0;
        var passed = ratio >= 0.5m && ratio <= 2.0m;

        return new ConfirmationResult
        {
            Name = "Volatility",
            Passed = passed,
            Weight = weight,
            Reason = passed
                ? $"ATR {currentAtr:F2} is {ratio:F2}x average ({avgAtr:F2}), within normal range"
                : $"ATR {currentAtr:F2} is {ratio:F2}x average ({avgAtr:F2}), outside 0.5x-2.0x range"
        };
    }

    /// <summary>
    /// MACD histogram direction aligns with the trade.
    /// Long: histogram > 0. Short: histogram &lt; 0.
    /// </summary>
    internal static ConfirmationResult CheckMacdHistogram(
        SignalDirection direction, CandleWithIndicators bar, decimal weight)
    {
        var histogram = bar.Indicators.MacdHistogram;

        // If MACD not computed (all zeros), fail
        if (histogram == 0 && bar.Indicators.MacdLine == 0 && bar.Indicators.MacdSignal == 0)
        {
            return new ConfirmationResult
            {
                Name = "MacdHistogram",
                Passed = false,
                Weight = weight,
                Reason = "MACD not available"
            };
        }

        var passed = direction == SignalDirection.Long ? histogram > 0 : histogram < 0;

        return new ConfirmationResult
        {
            Name = "MacdHistogram",
            Passed = passed,
            Weight = weight,
            Reason = passed
                ? $"MACD histogram {histogram:F4} aligns with {direction}"
                : $"MACD histogram {histogram:F4} opposes {direction}"
        };
    }

    /// <summary>
    /// Stochastic not at extreme against the trade.
    /// Long: Stochastic %K &lt; 80 (not overbought). Short: Stochastic %K &gt; 20 (not oversold).
    /// </summary>
    internal static ConfirmationResult CheckStochastic(
        SignalDirection direction, CandleWithIndicators bar, decimal weight)
    {
        var stochK = bar.Indicators.StochasticK;

        if (stochK == 0 && bar.Indicators.StochasticD == 0)
        {
            return new ConfirmationResult
            {
                Name = "Stochastic",
                Passed = false,
                Weight = weight,
                Reason = "Stochastic not available"
            };
        }

        bool passed;
        string reason;

        if (direction == SignalDirection.Long)
        {
            passed = stochK < 80m;
            reason = passed
                ? $"Stochastic %K {stochK:F1} not overbought (< 80)"
                : $"Stochastic %K {stochK:F1} overbought (>= 80), risky for long entry";
        }
        else
        {
            passed = stochK > 20m;
            reason = passed
                ? $"Stochastic %K {stochK:F1} not oversold (> 20)"
                : $"Stochastic %K {stochK:F1} oversold (<= 20), risky for short entry";
        }

        return new ConfirmationResult
        {
            Name = "Stochastic",
            Passed = passed,
            Weight = weight,
            Reason = reason
        };
    }
}
