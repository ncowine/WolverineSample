using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Evaluates strategy conditions against pre-computed indicator values.
/// Entry logic: ALL ConditionGroups must be true (AND). Within each group, ANY Condition (OR).
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// Evaluate whether all condition groups are satisfied for the given bar.
    /// </summary>
    public static bool Evaluate(
        List<ConditionGroup> groups,
        CandleWithIndicators bar,
        CandleWithIndicators? prevBar)
    {
        if (groups.Count == 0) return false;

        foreach (var group in groups)
        {
            var indicators = GetIndicatorsForTimeframe(bar, group.Timeframe);
            var prevIndicators = prevBar != null ? GetIndicatorsForTimeframe(prevBar, group.Timeframe) : null;

            if (indicators == null) return false;

            var anyTrue = false;
            foreach (var condition in group.Conditions)
            {
                if (EvaluateCondition(condition, bar, indicators, prevIndicators))
                {
                    anyTrue = true;
                    break;
                }
            }

            if (!anyTrue) return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluate with details: returns matched condition descriptions alongside bool result.
    /// </summary>
    public static (bool Passed, List<string> MatchedConditions) EvaluateWithDetails(
        List<ConditionGroup> groups,
        CandleWithIndicators bar,
        CandleWithIndicators? prevBar)
    {
        var matched = new List<string>();
        if (groups.Count == 0) return (false, matched);

        foreach (var group in groups)
        {
            var indicators = GetIndicatorsForTimeframe(bar, group.Timeframe);
            var prevIndicators = prevBar != null ? GetIndicatorsForTimeframe(prevBar, group.Timeframe) : null;

            if (indicators == null) return (false, matched);

            var anyTrue = false;
            foreach (var condition in group.Conditions)
            {
                if (EvaluateCondition(condition, bar, indicators, prevIndicators))
                {
                    var value = GetIndicatorValue(condition.Indicator, indicators, bar);
                    matched.Add($"{group.Timeframe}:{condition.Indicator} {condition.Comparison} {condition.Value} (actual={value:F2})");
                    anyTrue = true;
                    break;
                }
            }

            if (!anyTrue) return (false, matched);
        }

        return (true, matched);
    }

    internal static bool EvaluateCondition(
        Condition condition,
        CandleWithIndicators bar,
        IndicatorValues current,
        IndicatorValues? prev)
    {
        var value = GetIndicatorValue(condition.Indicator, current, bar);
        var prevValue = prev != null ? GetIndicatorValue(condition.Indicator, prev, bar) : (decimal?)null;

        var target = GetComparisonTarget(condition, current, bar);

        return condition.Comparison switch
        {
            "GreaterThan" or "IsAbove" => value > target,
            "LessThan" or "IsBelow" => value < target,
            "Between" => value >= condition.Value && condition.ValueHigh.HasValue && value <= condition.ValueHigh.Value,
            "CrossAbove" => prevValue.HasValue && prevValue.Value <= GetComparisonTarget(condition, prev!, bar) && value > target,
            "CrossBelow" => prevValue.HasValue && prevValue.Value >= GetComparisonTarget(condition, prev!, bar) && value < target,
            _ => false
        };
    }

    private static decimal GetComparisonTarget(Condition condition, IndicatorValues indicators, CandleWithIndicators bar)
    {
        // If there's a reference indicator, compare against it
        if (!string.IsNullOrEmpty(condition.ReferenceIndicator))
            return GetIndicatorValue(condition.ReferenceIndicator, indicators, bar);

        return condition.Value;
    }

    internal static decimal GetIndicatorValue(string indicator, IndicatorValues values, CandleWithIndicators bar)
    {
        return indicator switch
        {
            "RSI" => values.Rsi,
            "MACD" => values.MacdLine,
            "MACDSignal" => values.MacdSignal,
            "MACDHistogram" => values.MacdHistogram,
            "SMA" => values.SmaMedium, // default SMA
            "SMAShort" => values.SmaShort,
            "SMAMedium" => values.SmaMedium,
            "SMALong" => values.SmaLong,
            "EMA" => values.EmaMedium,
            "EMAShort" => values.EmaShort,
            "EMAMedium" => values.EmaMedium,
            "EMALong" => values.EmaLong,
            "WMA" => values.SmaMedium, // fallback to SMA
            "Stochastic" => values.StochasticK,
            "StochasticK" => values.StochasticK,
            "StochasticD" => values.StochasticD,
            "ATR" => values.Atr,
            "BollingerBands" => values.BollingerMiddle,
            "BollingerUpper" => values.BollingerUpper,
            "BollingerLower" => values.BollingerLower,
            "BollingerPercentB" => values.BollingerPercentB,
            "OBV" => values.Obv,
            "Volume" => bar.Volume,
            "Price" => bar.Close,
            _ => 0m
        };
    }

    private static IndicatorValues? GetIndicatorsForTimeframe(CandleWithIndicators bar, string timeframe)
    {
        if (timeframe == "Daily" || string.IsNullOrEmpty(timeframe))
            return bar.Indicators;

        if (timeframe == "Weekly" && bar.HigherTimeframeIndicators.TryGetValue(CandleInterval.Weekly, out var weekly))
            return weekly;

        if (timeframe == "Monthly" && bar.HigherTimeframeIndicators.TryGetValue(CandleInterval.Monthly, out var monthly))
            return monthly;

        // Fallback to daily if higher TF not available
        return bar.Indicators;
    }
}
