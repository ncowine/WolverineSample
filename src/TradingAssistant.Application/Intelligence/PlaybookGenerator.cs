using TradingAssistant.Contracts.Backtesting;

namespace TradingAssistant.Application.Intelligence;

/// <summary>
/// Generates hardcoded market-specific strategy templates (playbooks).
/// Three archetypes per market: Momentum, MeanReversion, Breakout.
/// Market-specific tuning: India uses wider stops, higher volume thresholds.
/// </summary>
public static class PlaybookGenerator
{
    public const string Momentum = "Momentum";
    public const string MeanReversion = "MeanReversion";
    public const string Breakout = "Breakout";

    public static readonly string[] TemplateTypes = [Momentum, MeanReversion, Breakout];

    /// <summary>
    /// Generate all 3 templates for a given market code.
    /// Returns (templateType, regimes, definition) tuples.
    /// </summary>
    public static IReadOnlyList<(string Type, string Regimes, StrategyDefinition Definition)> GenerateAll(
        string marketCode)
    {
        return
        [
            (Momentum, "Bull", BuildMomentum(marketCode)),
            (MeanReversion, "Sideways", BuildMeanReversion(marketCode)),
            (Breakout, "Bull,HighVolatility", BuildBreakout(marketCode))
        ];
    }

    public static StrategyDefinition BuildMomentum(string marketCode)
    {
        var isIndia = IsIndiaMarket(marketCode);

        return new StrategyDefinition
        {
            EntryConditions =
            [
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "EMA", Period = 20, Comparison = "CrossAbove", ReferenceIndicator = "EMA", ReferencePeriod = 50 },
                        new Condition { Indicator = "RSI", Period = 14, Comparison = "GreaterThan", Value = 50 }
                    ]
                },
                new ConditionGroup
                {
                    Timeframe = "Weekly",
                    Conditions =
                    [
                        new Condition { Indicator = "SMA", Period = 50, Comparison = "GreaterThan", ReferenceIndicator = "SMA", ReferencePeriod = 200 }
                    ]
                }
            ],
            ExitConditions =
            [
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "EMA", Period = 20, Comparison = "CrossBelow", ReferenceIndicator = "EMA", ReferencePeriod = 50 },
                        new Condition { Indicator = "RSI", Period = 14, Comparison = "LessThan", Value = 40 }
                    ]
                }
            ],
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = isIndia ? 3m : 2m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = isIndia ? 3m : 2.5m },
            PositionSizing = new PositionSizingConfig
            {
                SizingMethod = "Fixed",
                RiskPercent = isIndia ? 0.75m : 1m,
                MaxPositions = isIndia ? 4 : 6,
                MaxDrawdownPercent = isIndia ? 12m : 15m
            },
            Filters = new TradeFilterConfig
            {
                MinVolume = isIndia ? 500_000 : 200_000,
                MinPrice = isIndia ? 100m : 5m
            }
        };
    }

    public static StrategyDefinition BuildMeanReversion(string marketCode)
    {
        var isIndia = IsIndiaMarket(marketCode);

        return new StrategyDefinition
        {
            EntryConditions =
            [
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "RSI", Period = 14, Comparison = "LessThan", Value = isIndia ? 25m : 30m },
                        new Condition { Indicator = "BollingerBands", Period = 20, Comparison = "LessThan", Value = 0 }
                    ]
                },
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "SMA", Period = 200, Comparison = "GreaterThan", Value = 0, ReferenceIndicator = "Price" }
                    ]
                }
            ],
            ExitConditions =
            [
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "RSI", Period = 14, Comparison = "GreaterThan", Value = 60 },
                        new Condition { Indicator = "BollingerBands", Period = 20, Comparison = "GreaterThan", Value = 0 }
                    ]
                }
            ],
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = isIndia ? 2.5m : 1.5m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 2m },
            PositionSizing = new PositionSizingConfig
            {
                SizingMethod = "Fixed",
                RiskPercent = isIndia ? 0.5m : 0.75m,
                MaxPositions = isIndia ? 3 : 5,
                MaxDrawdownPercent = isIndia ? 10m : 12m
            },
            Filters = new TradeFilterConfig
            {
                MinVolume = isIndia ? 500_000 : 200_000,
                MinPrice = isIndia ? 100m : 5m
            }
        };
    }

    public static StrategyDefinition BuildBreakout(string marketCode)
    {
        var isIndia = IsIndiaMarket(marketCode);

        return new StrategyDefinition
        {
            EntryConditions =
            [
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "Price", Comparison = "GreaterThan", Value = 0, ReferenceIndicator = "BollingerBands", ReferencePeriod = 20 },
                        new Condition { Indicator = "Volume", Comparison = "GreaterThan", Value = isIndia ? 2m : 1.5m }
                    ]
                },
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "ATR", Period = 14, Comparison = "GreaterThan", Value = 0 }
                    ]
                }
            ],
            ExitConditions =
            [
                new ConditionGroup
                {
                    Timeframe = "Daily",
                    Conditions =
                    [
                        new Condition { Indicator = "Price", Comparison = "LessThan", Value = 0, ReferenceIndicator = "EMA", ReferencePeriod = 20 }
                    ]
                }
            ],
            StopLoss = new StopLossConfig { Type = "Atr", Multiplier = isIndia ? 3.5m : 2.5m },
            TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = isIndia ? 3m : 2m },
            PositionSizing = new PositionSizingConfig
            {
                SizingMethod = "Fixed",
                RiskPercent = isIndia ? 0.75m : 1m,
                MaxPositions = isIndia ? 4 : 5,
                MaxDrawdownPercent = isIndia ? 12m : 15m
            },
            Filters = new TradeFilterConfig
            {
                MinVolume = isIndia ? 1_000_000 : 500_000,
                MinPrice = isIndia ? 150m : 10m
            }
        };
    }

    private static bool IsIndiaMarket(string marketCode) =>
        marketCode.Equals("IN", StringComparison.OrdinalIgnoreCase) ||
        marketCode.StartsWith("IN_", StringComparison.OrdinalIgnoreCase);
}
