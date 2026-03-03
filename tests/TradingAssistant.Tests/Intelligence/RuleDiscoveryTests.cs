using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Intelligence;

public class RuleDiscoveryTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string BuildValidDiscoveryResponse()
    {
        return """
        {
          "discoveredRules": [
            {
              "rule": "RSI < 35 AND MACD_H > 0",
              "confidence": 0.82,
              "supportingTradeCount": 12,
              "description": "Oversold bounce with MACD confirmation"
            },
            {
              "rule": "SMA_slope > 0.005 AND Vol > 500000",
              "confidence": 0.71,
              "supportingTradeCount": 8,
              "description": "Strong uptrend with volume confirmation"
            },
            {
              "rule": "ATR < 1.5 AND RSI > 40 AND RSI < 60",
              "confidence": 0.65,
              "supportingTradeCount": 6,
              "description": "Low volatility range trades"
            },
            {
              "rule": "MACD_H > 0.5 AND SMA_slope > 0.002",
              "confidence": 0.60,
              "supportingTradeCount": 5,
              "description": "Strong momentum alignment"
            },
            {
              "rule": "RSI < 30 (avoid when Vol < 200000)",
              "confidence": 0.55,
              "supportingTradeCount": 4,
              "description": "Deep oversold only with adequate liquidity"
            }
          ],
          "patterns": [
            "Winners tend to have RSI below 40 at entry",
            "High volume trades outperform low volume by 3:1",
            "Losses concentrate when SMA slope is negative"
          ],
          "summary": "Found 5 distinguishing factors between winners and losers"
        }
        """;
    }

    /// <summary>
    /// Creates a TradeLogJson with the specified number of trades, each with indicator data.
    /// </summary>
    private static string CreateTradeLogJson(int count)
    {
        var rng = new Random(42); // deterministic seed
        var trades = new List<object>();
        for (var i = 0; i < count; i++)
        {
            trades.Add(new
            {
                symbol = i % 3 == 0 ? "AAPL" : i % 3 == 1 ? "MSFT" : "GOOGL",
                side = "Long",
                pnlPercent = Math.Round((rng.NextDouble() * 20) - 8, 2), // -8% to +12%
                entryRsi = Math.Round(rng.NextDouble() * 80 + 10, 1),    // 10 to 90
                entryMacdHistogram = Math.Round((rng.NextDouble() * 2) - 0.5, 3),
                entrySmaSlope = Math.Round((rng.NextDouble() * 0.01) - 0.003, 4),
                entryAtr = Math.Round(rng.NextDouble() * 3 + 0.5, 2),
                entryVolume = Math.Round(rng.NextDouble() * 2_000_000 + 100_000, 0)
            });
        }
        return JsonSerializer.Serialize(trades, JsonOpts);
    }

    #region DiscoverRulesHandler Tests

    [Fact]
    public async Task HandleAsync_SuccessfulDiscovery_SavesAndReturnsRules()
    {
        // Arrange
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        var strategy = new Strategy
        {
            Name = "MomentumUS",
            Description = "Momentum strategy for US market",
            IsActive = true
        };
        backtestDb.Strategies.Add(strategy);

        var run = new BacktestRun
        {
            StrategyId = strategy.Id,
            Symbol = "SPY",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2026, 1, 1),
            Status = BacktestRunStatus.Completed,
            Result = new BacktestResult
            {
                TotalTrades = 60,
                WinRate = 55m,
                TotalReturn = 12.5m,
                MaxDrawdown = 8m,
                SharpeRatio = 1.2m,
                TradeLogJson = CreateTradeLogJson(60)
            }
        };
        backtestDb.BacktestRuns.Add(run);
        await backtestDb.SaveChangesAsync();

        claude.SetDefaultResponse(BuildValidDiscoveryResponse());

        var command = new DiscoverRulesCommand(strategy.Id, "US_SP500");

        // Act
        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.DiscoveryId);
        Assert.Equal("MomentumUS", result.StrategyName);
        Assert.Equal(60, result.TradeCount);
        Assert.Equal(5, result.DiscoveredRules.Count);
        Assert.Equal(3, result.Patterns.Count);

        // Top rule
        Assert.Equal("RSI < 35 AND MACD_H > 0", result.DiscoveredRules[0].Rule);
        Assert.Equal(0.82m, result.DiscoveredRules[0].Confidence);
        Assert.Equal(12, result.DiscoveredRules[0].SupportingTradeCount);

        // Verify saved to DB
        var saved = await intelligenceDb.RuleDiscoveries.FindAsync(result.DiscoveryId);
        Assert.NotNull(saved);
        Assert.Equal(strategy.Id, saved!.StrategyId);
        Assert.Equal("US_SP500", saved.MarketCode);
        Assert.Equal(60, saved.TradeCount);
        Assert.False(saved.IsApproved); // Not auto-approved
    }

    [Fact]
    public async Task HandleAsync_InsufficientTrades_ReturnsError()
    {
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        var strategy = new Strategy { Name = "FewTrades", Description = "Test" };
        backtestDb.Strategies.Add(strategy);

        var run = new BacktestRun
        {
            StrategyId = strategy.Id,
            Symbol = "SPY",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2026, 1, 1),
            Status = BacktestRunStatus.Completed,
            Result = new BacktestResult
            {
                TotalTrades = 20,
                WinRate = 50m,
                TotalReturn = 5m,
                MaxDrawdown = 3m,
                SharpeRatio = 0.8m,
                TradeLogJson = CreateTradeLogJson(20)
            }
        };
        backtestDb.BacktestRuns.Add(run);
        await backtestDb.SaveChangesAsync();

        var command = new DiscoverRulesCommand(strategy.Id, "US_SP500");

        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        Assert.False(result.Success);
        Assert.Contains("Insufficient trades", result.Error);
        Assert.Contains("20", result.Error);
        Assert.Contains("50", result.Error);
        Assert.Equal(0, claude.CallCount); // No Claude call made
    }

    [Fact]
    public async Task HandleAsync_StrategyNotFound_ReturnsError()
    {
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        var command = new DiscoverRulesCommand(Guid.NewGuid(), "US_SP500");

        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task HandleAsync_RateLimited_ReturnsError()
    {
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient(maxDailyCalls: 0);

        var strategy = new Strategy { Name = "Test", Description = "Test" };
        backtestDb.Strategies.Add(strategy);
        var run = new BacktestRun
        {
            StrategyId = strategy.Id,
            Symbol = "SPY",
            StartDate = DateTime.UtcNow.AddYears(-2),
            EndDate = DateTime.UtcNow,
            Status = BacktestRunStatus.Completed,
            Result = new BacktestResult
            {
                TotalTrades = 60,
                TradeLogJson = CreateTradeLogJson(60)
            }
        };
        backtestDb.BacktestRuns.Add(run);
        await backtestDb.SaveChangesAsync();

        var command = new DiscoverRulesCommand(strategy.Id, "US_SP500");

        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        Assert.False(result.Success);
        Assert.Contains("rate limit", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_MultipleBacktestRuns_AggregatesTrades()
    {
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        var strategy = new Strategy { Name = "MultiRun", Description = "Test" };
        backtestDb.Strategies.Add(strategy);

        // Run 1: 30 trades
        backtestDb.BacktestRuns.Add(new BacktestRun
        {
            StrategyId = strategy.Id,
            Symbol = "SPY",
            StartDate = new DateTime(2024, 1, 1),
            EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Completed,
            Result = new BacktestResult
            {
                TotalTrades = 30,
                TradeLogJson = CreateTradeLogJson(30)
            }
        });

        // Run 2: 30 trades
        backtestDb.BacktestRuns.Add(new BacktestRun
        {
            StrategyId = strategy.Id,
            Symbol = "QQQ",
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2026, 1, 1),
            Status = BacktestRunStatus.Completed,
            Result = new BacktestResult
            {
                TotalTrades = 30,
                TradeLogJson = CreateTradeLogJson(30)
            }
        });
        await backtestDb.SaveChangesAsync();

        claude.SetDefaultResponse(BuildValidDiscoveryResponse());

        var command = new DiscoverRulesCommand(strategy.Id, "US_SP500");

        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        Assert.True(result.Success);
        Assert.Equal(60, result.TradeCount); // 30 + 30
    }

    [Fact]
    public async Task HandleAsync_NoBacktestResults_ReturnsInsufficientTrades()
    {
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        var strategy = new Strategy { Name = "NoResults", Description = "Test" };
        backtestDb.Strategies.Add(strategy);
        await backtestDb.SaveChangesAsync();

        var command = new DiscoverRulesCommand(strategy.Id, "US_SP500");

        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        Assert.False(result.Success);
        Assert.Contains("Insufficient trades", result.Error);
        Assert.Contains("0", result.Error);
    }

    #endregion

    #region ExtractTrades Tests

    [Fact]
    public void ExtractTrades_ValidJson_ReturnsAllTrades()
    {
        var json = CreateTradeLogJson(10);
        var trades = DiscoverRulesHandler.ExtractTrades(json);

        Assert.Equal(10, trades.Count);
        Assert.All(trades, t =>
        {
            Assert.NotEmpty(t.Symbol);
            Assert.NotEmpty(t.Side);
        });
    }

    [Fact]
    public void ExtractTrades_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(DiscoverRulesHandler.ExtractTrades(null));
        Assert.Empty(DiscoverRulesHandler.ExtractTrades(""));
        Assert.Empty(DiscoverRulesHandler.ExtractTrades("   "));
    }

    [Fact]
    public void ExtractTrades_InvalidJson_ReturnsEmpty()
    {
        Assert.Empty(DiscoverRulesHandler.ExtractTrades("not json"));
    }

    [Fact]
    public void ExtractTrades_NonArrayJson_ReturnsEmpty()
    {
        Assert.Empty(DiscoverRulesHandler.ExtractTrades("""{"key":"value"}"""));
    }

    [Fact]
    public void ExtractTrades_CorrectlyClassifiesWinners()
    {
        var json = """
        [
          {"symbol":"AAPL","side":"Long","pnlPercent":5.5,"entryRsi":32,"entryMacdHistogram":0.5,"entrySmaSlope":0.003,"entryAtr":1.5,"entryVolume":1000000},
          {"symbol":"MSFT","side":"Long","pnlPercent":-3.2,"entryRsi":65,"entryMacdHistogram":-0.2,"entrySmaSlope":-0.001,"entryAtr":2.0,"entryVolume":800000}
        ]
        """;

        var trades = DiscoverRulesHandler.ExtractTrades(json);

        Assert.Equal(2, trades.Count);
        Assert.True(trades[0].WonTrade);   // +5.5%
        Assert.False(trades[1].WonTrade);  // -3.2%
        Assert.Equal(32m, trades[0].EntryRsi);
        Assert.Equal(0.5m, trades[0].EntryMacdHistogram);
    }

    #endregion

    #region GetDiscoveredRulesHandler Tests

    [Fact]
    public async Task GetDiscoveredRules_ReturnsHistory()
    {
        var db = TestIntelligenceDbContextFactory.Create();
        var strategyId = Guid.NewGuid();

        db.RuleDiscoveries.AddRange(
            new RuleDiscovery
            {
                StrategyId = strategyId,
                StrategyName = "TestStrat",
                MarketCode = "US_SP500",
                TradeCount = 60,
                WinningTrades = 33,
                LosingTrades = 27,
                DiscoveredRulesJson = """[{"rule":"RSI < 30","confidence":0.8,"supportingTradeCount":10,"description":"Oversold bounce"}]""",
                PatternsJson = """["Low RSI entries outperform"]""",
                Summary = "Found 1 rule"
            },
            new RuleDiscovery
            {
                StrategyId = strategyId,
                StrategyName = "TestStrat",
                MarketCode = "US_SP500",
                TradeCount = 100,
                WinningTrades = 55,
                LosingTrades = 45,
                DiscoveredRulesJson = """[{"rule":"MACD_H > 0","confidence":0.7,"supportingTradeCount":8,"description":"Positive momentum"}]""",
                PatternsJson = """["MACD confirmation improves win rate"]""",
                Summary = "Found 1 rule (updated)"
            },
            // Different strategy — should not be returned
            new RuleDiscovery
            {
                StrategyId = Guid.NewGuid(),
                StrategyName = "OtherStrat",
                MarketCode = "IN_NIFTY50",
                TradeCount = 50,
                Summary = "Not this one"
            });
        await db.SaveChangesAsync();

        var result = await GetDiscoveredRulesHandler.HandleAsync(
            new GetDiscoveredRulesQuery(strategyId), db);

        Assert.Equal(2, result.Count);
        Assert.All(result, r =>
        {
            Assert.True(r.Success);
            Assert.Equal("TestStrat", r.StrategyName);
        });
    }

    [Fact]
    public async Task GetDiscoveredRules_NoResults_ReturnsEmpty()
    {
        var db = TestIntelligenceDbContextFactory.Create();

        var result = await GetDiscoveredRulesHandler.HandleAsync(
            new GetDiscoveredRulesQuery(Guid.NewGuid()), db);

        Assert.Empty(result);
    }

    #endregion

    #region Recommendations Not Auto-Applied

    [Fact]
    public async Task HandleAsync_RecommendationsNotAutoApplied_IsApprovedFalse()
    {
        var backtestDb = TestBacktestDbContextFactory.Create();
        var intelligenceDb = TestIntelligenceDbContextFactory.Create();
        var claude = new FakeClaudeClient();

        var strategy = new Strategy { Name = "NeedApproval", Description = "Test" };
        backtestDb.Strategies.Add(strategy);
        backtestDb.BacktestRuns.Add(new BacktestRun
        {
            StrategyId = strategy.Id,
            Symbol = "SPY",
            StartDate = DateTime.UtcNow.AddYears(-2),
            EndDate = DateTime.UtcNow,
            Status = BacktestRunStatus.Completed,
            Result = new BacktestResult
            {
                TotalTrades = 55,
                TradeLogJson = CreateTradeLogJson(55)
            }
        });
        await backtestDb.SaveChangesAsync();

        claude.SetDefaultResponse(BuildValidDiscoveryResponse());

        var command = new DiscoverRulesCommand(strategy.Id, "US_SP500");

        var result = await DiscoverRulesHandler.HandleAsync(
            command, claude, backtestDb, intelligenceDb,
            NullLogger<DiscoverRulesHandler>.Instance);

        Assert.True(result.Success);

        // Verify the discovery is saved as NOT approved — user must approve
        var saved = await intelligenceDb.RuleDiscoveries.FindAsync(result.DiscoveryId);
        Assert.False(saved!.IsApproved);
    }

    #endregion
}
