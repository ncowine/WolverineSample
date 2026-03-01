using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Backtesting;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Backtesting;

public class CreateStrategyV2HandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task Creates_v2_strategy_with_definition()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = BuildSampleDefinition();
        var command = new CreateStrategyV2Command("RSI Mean Reversion", "Buy oversold, sell overbought", definition);

        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal("RSI Mean Reversion", result.Name);
        Assert.Equal("Buy oversold, sell overbought", result.Description);
        Assert.True(result.IsActive);
        Assert.True(result.UsesV2Engine);
        Assert.NotNull(result.Definition);
    }

    [Fact]
    public async Task Stores_rules_json_in_database()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = BuildSampleDefinition();
        var command = new CreateStrategyV2Command("Test Strategy", null, definition);

        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        var stored = await db.Strategies.FirstAsync(s => s.Id == result.Id);
        Assert.NotNull(stored.RulesJson);
        Assert.True(stored.UsesV2Engine);

        // Verify JSON roundtrip
        var deserialized = JsonSerializer.Deserialize<StrategyDefinition>(stored.RulesJson!, JsonOptions);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.EntryConditions);
        Assert.Equal("RSI", deserialized.EntryConditions[0].Conditions[0].Indicator);
    }

    [Fact]
    public async Task Trims_name_and_description()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var command = new CreateStrategyV2Command("  Padded Name  ", "  desc  ", BuildSampleDefinition());

        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal("Padded Name", result.Name);
        Assert.Equal("desc", result.Description);
    }

    [Fact]
    public async Task Null_description_stored_as_empty()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var command = new CreateStrategyV2Command("Test", null, BuildSampleDefinition());

        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal(string.Empty, result.Description);
    }

    [Fact]
    public async Task Entry_condition_count_correct()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = new StrategyDefinition
        {
            EntryConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30 },
                    new() { Indicator = "Stochastic", Comparison = "LessThan", Value = 20 }
                }},
                new() { Timeframe = "Weekly", Conditions = new List<Condition>
                {
                    new() { Indicator = "EMA", Comparison = "GreaterThan", Value = 0, Period = 50, ReferenceIndicator = "EMA", ReferencePeriod = 200 }
                }}
            }
        };

        var command = new CreateStrategyV2Command("Multi-Condition", null, definition);
        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal(3, result.EntryConditionCount); // 2 in group 1 + 1 in group 2
    }

    [Fact]
    public async Task Stop_loss_description_formatted()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = BuildSampleDefinition();
        definition.StopLoss = new StopLossConfig { Type = "Atr", Multiplier = 2.5m };

        var command = new CreateStrategyV2Command("Test", null, definition);
        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal("2.5x ATR", result.StopLossDescription);
    }

    [Fact]
    public async Task Take_profit_description_formatted()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = BuildSampleDefinition();
        definition.TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 3m };

        var command = new CreateStrategyV2Command("Test", null, definition);
        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal("3R", result.TakeProfitDescription);
    }

    [Fact]
    public async Task Position_sizing_preserved_in_roundtrip()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = BuildSampleDefinition();
        definition.PositionSizing = new PositionSizingConfig
        {
            RiskPercent = 1.5m,
            MaxPositions = 4,
            MaxPortfolioHeat = 8m,
            MaxDrawdownPercent = 12m
        };

        var command = new CreateStrategyV2Command("Test", null, definition);
        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.NotNull(result.Definition);
        Assert.Equal(1.5m, result.Definition!.PositionSizing.RiskPercent);
        Assert.Equal(4, result.Definition.PositionSizing.MaxPositions);
        Assert.Equal(8m, result.Definition.PositionSizing.MaxPortfolioHeat);
        Assert.Equal(12m, result.Definition.PositionSizing.MaxDrawdownPercent);
    }

    [Fact]
    public async Task Filters_preserved_in_roundtrip()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = BuildSampleDefinition();
        definition.Filters = new TradeFilterConfig
        {
            MinVolume = 500_000,
            MinPrice = 10m,
            MaxPrice = 500m,
            Sectors = new List<string> { "Technology", "Healthcare" }
        };

        var command = new CreateStrategyV2Command("Test", null, definition);
        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.NotNull(result.Definition?.Filters);
        Assert.Equal(500_000, result.Definition!.Filters.MinVolume);
        Assert.Equal(10m, result.Definition.Filters.MinPrice);
        Assert.Equal(2, result.Definition.Filters.Sectors!.Count);
    }

    [Fact]
    public async Task Backward_compatible_v1_strategy_not_v2()
    {
        using var db = TestBacktestDbContextFactory.Create();

        // Create a legacy v1 strategy (no RulesJson)
        var legacy = new TradingAssistant.Domain.Backtesting.Strategy
        {
            Name = "Legacy SMA Crossover",
            Description = "Old-style strategy"
        };
        db.Strategies.Add(legacy);
        await db.SaveChangesAsync();

        var stored = await db.Strategies.FirstAsync(s => s.Id == legacy.Id);
        Assert.False(stored.UsesV2Engine);
        Assert.Null(stored.RulesJson);
    }

    [Fact]
    public async Task DeserializeDefinition_returns_null_for_empty()
    {
        Assert.Null(CreateStrategyV2Handler.DeserializeDefinition(null));
        Assert.Null(CreateStrategyV2Handler.DeserializeDefinition(""));
        Assert.Null(CreateStrategyV2Handler.DeserializeDefinition("   "));
    }

    [Fact]
    public async Task Complex_strategy_with_exit_conditions()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var definition = new StrategyDefinition
        {
            EntryConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30 }
                }}
            },
            ExitConditions = new List<ConditionGroup>
            {
                new() { Timeframe = "Daily", Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 70 },
                    new() { Indicator = "MACD", Comparison = "CrossBelow", Value = 0 }
                }}
            },
            StopLoss = new StopLossConfig { Type = "FixedPercent", Multiplier = 5m },
            TakeProfit = new TakeProfitConfig { Type = "FixedPercent", Multiplier = 15m }
        };

        var command = new CreateStrategyV2Command("RSI with Exit", null, definition);
        var result = await CreateStrategyV2Handler.HandleAsync(command, db);

        Assert.Equal(1, result.EntryConditionCount);
        Assert.Equal(2, result.ExitConditionCount);
        Assert.Equal("5%", result.StopLossDescription);
        Assert.Equal("15%", result.TakeProfitDescription);
    }

    // --- Helper ---

    private static StrategyDefinition BuildSampleDefinition() => new()
    {
        EntryConditions = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30, Period = 14 }
                }
            }
        },
        ExitConditions = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 70, Period = 14 }
                }
            }
        },
        StopLoss = new StopLossConfig { Type = "Atr", Multiplier = 2m },
        TakeProfit = new TakeProfitConfig { Type = "RMultiple", Multiplier = 2m },
        PositionSizing = new PositionSizingConfig
        {
            RiskPercent = 1m,
            MaxPositions = 6,
            MaxPortfolioHeat = 6m,
            MaxDrawdownPercent = 15m
        }
    };
}
