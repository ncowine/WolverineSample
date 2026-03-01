using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Tests.Backtesting;

public class ConditionEvaluatorTests
{
    private static CandleWithIndicators MakeBar(
        decimal close = 100m,
        decimal rsi = 50m,
        decimal macdLine = 0m,
        decimal macdSignal = 0m,
        decimal smaShort = 100m,
        decimal smaMedium = 100m,
        decimal emaShort = 100m,
        decimal emaMedium = 100m,
        decimal atr = 2m,
        decimal bollingerUpper = 110m,
        decimal bollingerLower = 90m,
        long volume = 1_000_000)
    {
        return new CandleWithIndicators
        {
            Timestamp = new DateTime(2025, 6, 1),
            Open = close - 1,
            High = close + 2,
            Low = close - 2,
            Close = close,
            Volume = volume,
            Interval = CandleInterval.Daily,
            Indicators = new IndicatorValues
            {
                Rsi = rsi,
                MacdLine = macdLine,
                MacdSignal = macdSignal,
                MacdHistogram = macdLine - macdSignal,
                SmaShort = smaShort,
                SmaMedium = smaMedium,
                EmaShort = emaShort,
                EmaMedium = emaMedium,
                Atr = atr,
                BollingerUpper = bollingerUpper,
                BollingerLower = bollingerLower,
                BollingerMiddle = (bollingerUpper + bollingerLower) / 2m,
                IsWarmedUp = true
            }
        };
    }

    [Fact]
    public void Empty_groups_returns_false()
    {
        var bar = MakeBar();
        Assert.False(ConditionEvaluator.Evaluate(new List<ConditionGroup>(), bar, null));
    }

    [Fact]
    public void GreaterThan_comparison_works()
    {
        var bar = MakeBar(rsi: 70);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 60 }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void LessThan_comparison_works()
    {
        var bar = MakeBar(rsi: 25);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30 }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void Between_comparison_works()
    {
        var bar = MakeBar(rsi: 50);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "Between", Value = 40, ValueHigh = 60 }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void Between_fails_when_outside_range()
    {
        var bar = MakeBar(rsi: 75);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "Between", Value = 40, ValueHigh = 60 }
                }
            }
        };

        Assert.False(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void CrossAbove_detected()
    {
        var prevBar = MakeBar(macdLine: -1, macdSignal: 0);
        var bar = MakeBar(macdLine: 1, macdSignal: 0);

        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "MACD", Comparison = "CrossAbove", ReferenceIndicator = "MACDSignal" }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, prevBar));
    }

    [Fact]
    public void CrossBelow_detected()
    {
        var prevBar = MakeBar(macdLine: 1, macdSignal: 0);
        var bar = MakeBar(macdLine: -1, macdSignal: 0);

        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "MACD", Comparison = "CrossBelow", ReferenceIndicator = "MACDSignal" }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, prevBar));
    }

    [Fact]
    public void CrossAbove_requires_prev_bar()
    {
        var bar = MakeBar(macdLine: 1, macdSignal: 0);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "MACD", Comparison = "CrossAbove", ReferenceIndicator = "MACDSignal" }
                }
            }
        };

        Assert.False(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void AND_logic_across_groups()
    {
        // Group 1: RSI > 60 (true), Group 2: RSI < 30 (false) → overall false
        var bar = MakeBar(rsi: 70);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 60 }
                }
            },
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30 }
                }
            }
        };

        Assert.False(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void OR_logic_within_group()
    {
        // RSI > 80 (false) OR RSI < 30 (true, since RSI=25) → group passes
        var bar = MakeBar(rsi: 25);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 80 },
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30 }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void Reference_indicator_comparison()
    {
        // Price > SMAMedium
        var bar = MakeBar(close: 110, smaMedium: 100);
        var groups = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "Price", Comparison = "GreaterThan", ReferenceIndicator = "SMAMedium" }
                }
            }
        };

        Assert.True(ConditionEvaluator.Evaluate(groups, bar, null));
    }

    [Fact]
    public void GetIndicatorValue_maps_all_known_indicators()
    {
        var bar = MakeBar();
        var values = bar.Indicators;
        values.Rsi = 55;
        values.StochasticK = 80;
        values.StochasticD = 75;
        values.Obv = 12345;

        Assert.Equal(55m, ConditionEvaluator.GetIndicatorValue("RSI", values, bar));
        Assert.Equal(80m, ConditionEvaluator.GetIndicatorValue("StochasticK", values, bar));
        Assert.Equal(75m, ConditionEvaluator.GetIndicatorValue("StochasticD", values, bar));
        Assert.Equal(12345m, ConditionEvaluator.GetIndicatorValue("OBV", values, bar));
        Assert.Equal(bar.Close, ConditionEvaluator.GetIndicatorValue("Price", values, bar));
        Assert.Equal(bar.Volume, ConditionEvaluator.GetIndicatorValue("Volume", values, bar));
        Assert.Equal(0m, ConditionEvaluator.GetIndicatorValue("UnknownIndicator", values, bar));
    }
}
