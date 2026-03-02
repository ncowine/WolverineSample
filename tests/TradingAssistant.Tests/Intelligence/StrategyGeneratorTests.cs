using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

#region StrategyDefinitionPrompt Tests

public class StrategyDefinitionPromptTests
{
    [Fact]
    public void BuildSystemPrompt_ContainsKeyInstructions()
    {
        var prompt = StrategyDefinitionPrompt.BuildSystemPrompt();

        Assert.Contains("quantitative trading strategist", prompt);
        Assert.Contains("StrategyDefinition", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAllInputFields()
    {
        var input = new StrategyDefinitionInput(
            "Momentum strategy using RSI and MACD",
            "AAPL",
            "Bull",
            15m,
            0.5m,
            "Only long positions");

        var prompt = StrategyDefinitionPrompt.BuildUserPrompt(input);

        Assert.Contains("AAPL", prompt);
        Assert.Contains("Bull", prompt);
        Assert.Contains("Momentum strategy using RSI and MACD", prompt);
        Assert.Contains("15%", prompt);
        Assert.Contains("0.5", prompt);
        Assert.Contains("Only long positions", prompt);
        Assert.Contains("RSI", prompt);
        Assert.Contains("MACD", prompt);
        Assert.Contains("CrossAbove", prompt);
    }

    [Fact]
    public void BuildUserPrompt_HandlesNullConstraints()
    {
        var input = new StrategyDefinitionInput("Test", "SPY", "Bear", 10m, 1.0m);

        var prompt = StrategyDefinitionPrompt.BuildUserPrompt(input);

        Assert.Contains("None", prompt);
    }

    [Fact]
    public void ParseResponse_ValidJson_ReturnsDefinition()
    {
        var json = """
        {
            "entryConditions": [
                {
                    "timeframe": "Daily",
                    "conditions": [
                        {
                            "indicator": "RSI",
                            "comparison": "LessThan",
                            "value": 30,
                            "period": 14
                        }
                    ]
                }
            ],
            "exitConditions": [
                {
                    "timeframe": "Daily",
                    "conditions": [
                        {
                            "indicator": "RSI",
                            "comparison": "GreaterThan",
                            "value": 70,
                            "period": 14
                        }
                    ]
                }
            ],
            "stopLoss": { "type": "Atr", "multiplier": 2 },
            "takeProfit": { "type": "RMultiple", "multiplier": 3 },
            "positionSizing": {
                "sizingMethod": "Fixed",
                "riskPercent": 1,
                "maxPositions": 4
            },
            "filters": { "minVolume": 100000 }
        }
        """;

        var result = StrategyDefinitionPrompt.ParseResponse(json);

        Assert.NotNull(result);
        Assert.Single(result!.EntryConditions);
        Assert.Single(result.ExitConditions);
        Assert.Equal("RSI", result.EntryConditions[0].Conditions[0].Indicator);
        Assert.Equal(30, result.EntryConditions[0].Conditions[0].Value);
        Assert.Equal("Atr", result.StopLoss.Type);
        Assert.Equal(3, result.TakeProfit.Multiplier);
    }

    [Fact]
    public void ParseResponse_JsonWithSurroundingText_ExtractsCorrectly()
    {
        var json = """
        Here is the strategy:
        {"entryConditions":[],"exitConditions":[],"stopLoss":{"type":"Atr","multiplier":2},"takeProfit":{"type":"RMultiple","multiplier":2},"positionSizing":{"sizingMethod":"Fixed","riskPercent":1,"maxPositions":6,"maxPortfolioHeat":6,"maxDrawdownPercent":15},"filters":{}}
        I hope this helps!
        """;

        var result = StrategyDefinitionPrompt.ParseResponse(json);
        Assert.NotNull(result);
        Assert.Equal("Atr", result!.StopLoss.Type);
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsNull()
    {
        var result = StrategyDefinitionPrompt.ParseResponse("this is not json");
        Assert.Null(result);
    }
}

#endregion

#region StrategyGenerator Tests

public class StrategyGeneratorTests
{
    private static StrategyDefinition CreateTestDefinition() => new()
    {
        EntryConditions =
        [
            new ConditionGroup
            {
                Timeframe = "Daily",
                Conditions =
                [
                    new Condition
                    {
                        Indicator = "RSI",
                        Comparison = "LessThan",
                        Value = 30,
                        Period = 14
                    }
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
                    new Condition
                    {
                        Indicator = "RSI",
                        Comparison = "GreaterThan",
                        Value = 70,
                        Period = 14
                    }
                ]
            }
        ],
        StopLoss = new StopLossConfig { Type = "FixedPercent", Multiplier = 5m },
        TakeProfit = new TakeProfitConfig { Type = "FixedPercent", Multiplier = 10m }
    };

    private static List<PriceCandle> CreateTestCandles(int count = 500)
    {
        var candles = new List<PriceCandle>();
        var rng = new Random(42); // Deterministic seed
        var basePrice = 100m;

        for (var i = 0; i < count; i++)
        {
            // Random walk with slight upward drift
            var change = (decimal)(rng.NextDouble() * 4 - 1.8);
            basePrice = Math.Max(10, basePrice + change);

            var open = basePrice;
            var high = basePrice + (decimal)(rng.NextDouble() * 3);
            var low = basePrice - (decimal)(rng.NextDouble() * 3);
            var close = basePrice + (decimal)(rng.NextDouble() * 2 - 1);
            close = Math.Max(low, Math.Min(high, close));

            candles.Add(new PriceCandle
            {
                StockId = Guid.NewGuid(),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1_000_000 + rng.Next(500_000),
                Timestamp = DateTime.UtcNow.AddDays(-count + i),
                Interval = CandleInterval.Daily
            });
        }

        return candles;
    }

    [Fact]
    public async Task GenerateAsync_WhenRateLimited_ReturnsError()
    {
        var claude = new FakeClaudeClient(maxDailyCalls: 0);
        var input = new StrategyDefinitionInput("Test", "AAPL", "Bull", 15m, 0.5m);

        var (definition, rationale, error) = await StrategyGenerator.GenerateAsync(claude, input);

        Assert.Null(definition);
        Assert.NotNull(error);
        Assert.Contains("rate limit", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_WhenClaudeFails_ReturnsError()
    {
        var claude = new FakeClaudeClient();
        // Don't set any response — default returns "{}" which won't parse correctly
        // Actually we need Claude to return a failure
        var failClaude = new FailingClaudeClient();

        var input = new StrategyDefinitionInput("Test", "AAPL", "Bull", 15m, 0.5m);
        var (definition, _, error) = await StrategyGenerator.GenerateAsync(failClaude, input);

        Assert.Null(definition);
        Assert.NotNull(error);
        Assert.Contains("Claude API error", error);
    }

    [Fact]
    public async Task GenerateAsync_WithValidResponse_ReturnsDefinition()
    {
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse("""
        {
            "entryConditions": [{"timeframe":"Daily","conditions":[{"indicator":"RSI","comparison":"LessThan","value":30,"period":14}]}],
            "exitConditions": [{"timeframe":"Daily","conditions":[{"indicator":"RSI","comparison":"GreaterThan","value":70,"period":14}]}],
            "stopLoss": {"type":"Atr","multiplier":2},
            "takeProfit": {"type":"RMultiple","multiplier":2},
            "positionSizing": {"sizingMethod":"Fixed","riskPercent":1,"maxPositions":6},
            "filters": {}
        }
        """);

        var input = new StrategyDefinitionInput("RSI mean reversion", "AAPL", "Bull", 15m, 0.5m);
        var (definition, rationale, error) = await StrategyGenerator.GenerateAsync(claude, input);

        Assert.NotNull(definition);
        Assert.Null(error);
        Assert.Single(definition!.EntryConditions);
        Assert.Equal("RSI", definition.EntryConditions[0].Conditions[0].Indicator);
    }

    [Fact]
    public void Backtest_WithValidData_ReturnsMetrics()
    {
        var definition = CreateTestDefinition();
        var candles = CreateTestCandles(500);

        var (result, metrics) = StrategyGenerator.Backtest(definition, candles, "TEST");

        Assert.Equal("TEST", result.Symbol);
        Assert.Equal(100_000m, result.InitialCapital);
        // Metrics should be computed (may or may not have trades depending on data)
        Assert.NotNull(metrics);
    }

    [Fact]
    public void Backtest_WithInsufficientData_ReturnsEmptyResult()
    {
        var definition = CreateTestDefinition();
        var candles = new List<PriceCandle>(); // Empty

        var (result, metrics) = StrategyGenerator.Backtest(definition, candles, "TEST");

        Assert.Equal(100_000m, result.FinalEquity);
        Assert.Equal(0, metrics.TotalTrades);
    }

    [Fact]
    public void Validate_ZeroTrades_Rejected()
    {
        var metrics = new PerformanceMetrics { TotalTrades = 0, SharpeRatio = 0 };

        var (accepted, reason) = StrategyGenerator.Validate(metrics, 0.5m);

        Assert.False(accepted);
        Assert.Contains("zero trades", reason!);
    }

    [Fact]
    public void Validate_LowSharpe_Rejected()
    {
        var metrics = new PerformanceMetrics
        {
            TotalTrades = 10,
            SharpeRatio = 0.3m
        };

        var (accepted, reason) = StrategyGenerator.Validate(metrics, 0.5m);

        Assert.False(accepted);
        Assert.Contains("0.30", reason!);
        Assert.Contains("0.50", reason);
    }

    [Fact]
    public void Validate_GoodSharpe_Accepted()
    {
        var metrics = new PerformanceMetrics
        {
            TotalTrades = 10,
            SharpeRatio = 1.5m
        };

        var (accepted, reason) = StrategyGenerator.Validate(metrics, 0.5m);

        Assert.True(accepted);
        Assert.Null(reason);
    }

    [Fact]
    public void ToSummary_MapsCorrectly()
    {
        var metrics = new PerformanceMetrics
        {
            TotalTrades = 42,
            WinRate = 55.5555m,
            TotalReturn = 23.456789m,
            MaxDrawdownPercent = -12.347m,
            SharpeRatio = 1.234567m
        };

        var summary = StrategyGenerator.ToSummary(metrics);

        Assert.Equal(42, summary.TotalTrades);
        Assert.Equal(55.56m, summary.WinRate);
        Assert.Equal(23.46m, summary.TotalReturn);
        Assert.Equal(-12.35m, summary.MaxDrawdown); // -12.347 rounds to -12.35
        Assert.Equal(1.23m, summary.SharpeRatio);
    }
}

#endregion

#region GenerateStrategyHandler Integration Tests

public class GenerateStrategyHandlerTests
{
    private static readonly string ValidStrategyJson = """
        {
            "entryConditions": [{"timeframe":"Daily","conditions":[{"indicator":"RSI","comparison":"LessThan","value":30,"period":14}]}],
            "exitConditions": [{"timeframe":"Daily","conditions":[{"indicator":"RSI","comparison":"GreaterThan","value":70,"period":14}]}],
            "stopLoss": {"type":"FixedPercent","multiplier":5},
            "takeProfit": {"type":"FixedPercent","multiplier":10},
            "positionSizing": {"sizingMethod":"Fixed","riskPercent":1,"maxPositions":6},
            "filters": {}
        }
        """;

    private static Guid SeedMarketData(
        Infrastructure.Persistence.MarketDataDbContext db,
        string symbol = "AAPL",
        int days = 500)
    {
        var stock = new Stock
        {
            Symbol = symbol,
            Name = $"{symbol} Inc.",
            Exchange = "NASDAQ",
            Sector = "Technology",
            IsActive = true
        };
        db.Stocks.Add(stock);

        var rng = new Random(42);
        var basePrice = 150m;

        for (var i = 0; i < days; i++)
        {
            var change = (decimal)(rng.NextDouble() * 4 - 1.8);
            basePrice = Math.Max(50, basePrice + change);
            var high = basePrice + (decimal)(rng.NextDouble() * 3);
            var low = basePrice - (decimal)(rng.NextDouble() * 3);
            var close = Math.Max(low, Math.Min(high, basePrice + (decimal)(rng.NextDouble() * 2 - 1)));

            db.PriceCandles.Add(new PriceCandle
            {
                StockId = stock.Id,
                Open = basePrice,
                High = high,
                Low = low,
                Close = close,
                Volume = 1_000_000 + rng.Next(500_000),
                Timestamp = DateTime.UtcNow.AddDays(-days + i),
                Interval = CandleInterval.Daily
            });
        }

        db.SaveChanges();
        return stock.Id;
    }

    [Fact]
    public async Task HandleAsync_RateLimited_ReturnsFailure()
    {
        var claude = new FakeClaudeClient(maxDailyCalls: 0);
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        var command = new GenerateStrategyCommand("Test strategy", "AAPL", "Bull");

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        Assert.False(result.Success);
        Assert.False(result.Rejected);
        Assert.Contains("rate limit", result.RejectionReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_UnknownSymbol_ReturnsFailure()
    {
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(ValidStrategyJson);
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        var command = new GenerateStrategyCommand("Test", "UNKNOWN", "Bull");

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        Assert.False(result.Success);
        Assert.Contains("No market data found", result.RejectionReason!);
    }

    [Fact]
    public async Task HandleAsync_InsufficientData_ReturnsFailure()
    {
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(ValidStrategyJson);
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        // Seed only 10 candles — below the 50 minimum
        SeedMarketData(marketDb, "AAPL", 10);

        var command = new GenerateStrategyCommand("Test", "AAPL", "Bull");

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        Assert.False(result.Success);
        Assert.Contains("Insufficient", result.RejectionReason!);
    }

    [Fact]
    public async Task HandleAsync_ValidFlow_RunsBacktestAndReturnsResult()
    {
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(ValidStrategyJson);
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        SeedMarketData(marketDb, "AAPL", 500);

        var command = new GenerateStrategyCommand("Momentum RSI strategy", "AAPL", "Bull");

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        // The result will be either success or rejected — both are valid outcomes
        // depending on the random-walk data. Key is that it ran without errors.
        Assert.NotNull(result);
        Assert.NotNull(result.BacktestMetrics);
        Assert.NotNull(result.Rationale);

        if (result.Success)
        {
            Assert.NotNull(result.Strategy);
            Assert.False(result.Rejected);
            // Strategy should have been saved to DB
            Assert.Single(backtestDb.Strategies);
        }
        else
        {
            Assert.True(result.Rejected);
            Assert.NotNull(result.RejectionReason);
            // No strategy saved
            Assert.Empty(backtestDb.Strategies);
        }
    }

    [Fact]
    public async Task HandleAsync_ClaudeApiFailure_ReturnsError()
    {
        var claude = new FailingClaudeClient();
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        SeedMarketData(marketDb, "AAPL", 500);

        var command = new GenerateStrategyCommand("Test", "AAPL", "Bull");

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        Assert.False(result.Success);
        Assert.False(result.Rejected);
        Assert.Contains("Claude API error", result.RejectionReason!);
    }

    [Fact]
    public async Task HandleAsync_InvalidJsonFromClaude_ReturnsParseError()
    {
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse("This is not valid JSON at all");
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        SeedMarketData(marketDb, "AAPL", 500);

        var command = new GenerateStrategyCommand("Test", "AAPL", "Bull");

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        Assert.False(result.Success);
        Assert.Contains("parse", result.RejectionReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_AcceptedStrategy_SavedWithCorrectName()
    {
        // Use a strategy with very generous thresholds to increase chance of acceptance
        var claude = new FakeClaudeClient();
        claude.SetDefaultResponse(ValidStrategyJson);
        using var marketDb = TestMarketDataDbContextFactory.Create();
        using var backtestDb = TestBacktestDbContextFactory.Create();
        var logger = NullLogger<GenerateStrategyHandler>.Instance;

        SeedMarketData(marketDb, "SPY", 500);

        // Very low target Sharpe to maximize chance of acceptance
        var command = new GenerateStrategyCommand(
            "Conservative strategy",
            "SPY",
            "Sideways",
            MaxDrawdownPercent: 30m,
            TargetSharpe: -99m); // Accept anything

        var result = await GenerateStrategyHandler.HandleAsync(
            command, claude, marketDb, backtestDb, logger);

        // With target Sharpe of -99, any strategy with trades should be accepted
        if (result.BacktestMetrics?.TotalTrades > 0)
        {
            Assert.True(result.Success);
            Assert.NotNull(result.Strategy);
            Assert.Contains("AI-SPY-Sideways", result.Strategy!.Name);
            Assert.Contains("[AI Generated]", result.Strategy.Description);
        }
    }
}

#endregion

#region Test Helpers

/// <summary>
/// Claude client that always returns failure responses.
/// </summary>
internal class FailingClaudeClient : IClaudeClient
{
    public int RemainingCallsToday => 50;
    public bool IsRateLimited => false;

    public Task<ClaudeResponse> CompleteAsync(ClaudeRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new ClaudeResponse(
            Content: string.Empty,
            InputTokens: 0,
            OutputTokens: 0,
            Success: false,
            Error: "Simulated API failure"));
    }
}

#endregion
