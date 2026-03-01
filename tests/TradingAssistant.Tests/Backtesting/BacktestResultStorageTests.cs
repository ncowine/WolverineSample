using System.Text.Json;
using TradingAssistant.Application.Backtesting;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Backtesting;

public class BacktestResultStorageTests
{
    [Fact]
    public async Task New_fields_persist_and_load()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow };
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(), StrategyId = strategy.Id, Symbol = "AAPL",
            StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Completed, CreatedAt = DateTime.UtcNow
        };

        var equityCurveJson = JsonSerializer.Serialize(
            Enumerable.Range(0, 100).Select(i => new { Date = DateTime.Today.AddDays(i), Value = 100000m + i * 10m }));
        var compressedEquity = JsonCompression.Compress(equityCurveJson);

        var tradeLogJson = JsonSerializer.Serialize(
            Enumerable.Range(0, 50).Select(i => new { Symbol = "AAPL", PnL = i * 10m }));
        var compressedTrades = JsonCompression.Compress(tradeLogJson);

        var result = new BacktestResult
        {
            Id = Guid.NewGuid(),
            BacktestRunId = run.Id,
            TotalTrades = 50,
            WinRate = 60m,
            TotalReturn = 45m,
            MaxDrawdown = 10m,
            SharpeRatio = 1.5m,
            Cagr = 8.5m,
            SortinoRatio = 2.1m,
            CalmarRatio = 0.85m,
            ProfitFactor = 2.0m,
            Expectancy = 75m,
            OverfittingScore = 0.22m,
            EquityCurveJson = compressedEquity,
            TradeLogJson = compressedTrades,
            MonthlyReturnsJson = "{\"2024-01\":2.3,\"2024-02\":-1.1}",
            BenchmarkReturnJson = "{\"SpyCagr\":10.5}",
            ParametersJson = "{\"SmaShort\":10,\"SmaLong\":50}",
            WalkForwardJson = "{\"Windows\":5,\"AvgEfficiency\":0.7}",
            SpyComparisonJson = "{\"StrategyCagr\":8.5,\"SpyCagr\":10.5,\"Alpha\":-2,\"Beta\":0.8}",
            CreatedAt = DateTime.UtcNow
        };

        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(run);
        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        // Reload from DB
        var loaded = db.BacktestResults.Single(r => r.Id == result.Id);

        Assert.Equal(50, loaded.TotalTrades);
        Assert.Equal(8.5m, loaded.Cagr);
        Assert.Equal(2.1m, loaded.SortinoRatio);
        Assert.Equal(0.85m, loaded.CalmarRatio);
        Assert.Equal(2.0m, loaded.ProfitFactor);
        Assert.Equal(75m, loaded.Expectancy);
        Assert.Equal(0.22m, loaded.OverfittingScore);
        Assert.Contains("SmaShort", loaded.ParametersJson);
        Assert.Contains("SpyCagr", loaded.SpyComparisonJson);
    }

    [Fact]
    public async Task Compressed_equity_curve_roundtrips()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow };
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(), StrategyId = strategy.Id, Symbol = "AAPL",
            StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Completed, CreatedAt = DateTime.UtcNow
        };

        // Simulate 1260 equity points (5 years)
        var points = Enumerable.Range(0, 1260)
            .Select(i => new { Date = new DateTime(2020, 1, 2).AddDays(i).ToString("yyyy-MM-dd"), Value = 100000m + i * 8m })
            .ToList();
        var originalJson = JsonSerializer.Serialize(points);
        var compressed = JsonCompression.Compress(originalJson);

        var result = new BacktestResult
        {
            Id = Guid.NewGuid(), BacktestRunId = run.Id,
            EquityCurveJson = compressed,
            CreatedAt = DateTime.UtcNow
        };

        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(run);
        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        var loaded = db.BacktestResults.Single(r => r.Id == result.Id);
        var decompressed = JsonCompression.Decompress(loaded.EquityCurveJson);

        Assert.Equal(originalJson, decompressed);
    }

    [Fact]
    public async Task Compressed_trade_log_roundtrips()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow };
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(), StrategyId = strategy.Id, Symbol = "MSFT",
            StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Completed, CreatedAt = DateTime.UtcNow
        };

        // Simulate 500 trades
        var trades = Enumerable.Range(0, 500)
            .Select(i => new
            {
                Symbol = "MSFT",
                EntryDate = new DateTime(2020, 1, 2).AddDays(i * 3).ToString("yyyy-MM-dd"),
                EntryPrice = 200m + i * 0.5m,
                ExitDate = new DateTime(2020, 1, 5).AddDays(i * 3).ToString("yyyy-MM-dd"),
                ExitPrice = 202m + i * 0.5m,
                PnL = 200m,
                HoldingDays = 3
            })
            .ToList();
        var originalJson = JsonSerializer.Serialize(trades);
        var compressed = JsonCompression.Compress(originalJson);

        var result = new BacktestResult
        {
            Id = Guid.NewGuid(), BacktestRunId = run.Id,
            TradeLogJson = compressed,
            CreatedAt = DateTime.UtcNow
        };

        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(run);
        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        var loaded = db.BacktestResults.Single(r => r.Id == result.Id);
        var decompressed = JsonCompression.Decompress(loaded.TradeLogJson);

        Assert.Equal(originalJson, decompressed);
    }

    [Fact]
    public async Task Overfitting_score_nullable()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var strategy = new Strategy { Id = Guid.NewGuid(), Name = "Test", CreatedAt = DateTime.UtcNow };
        var run = new BacktestRun
        {
            Id = Guid.NewGuid(), StrategyId = strategy.Id, Symbol = "AAPL",
            StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Completed, CreatedAt = DateTime.UtcNow
        };

        // Result without optimization — OverfittingScore is null
        var result = new BacktestResult
        {
            Id = Guid.NewGuid(), BacktestRunId = run.Id,
            TotalTrades = 10, WinRate = 50m, TotalReturn = 5m,
            MaxDrawdown = 8m, SharpeRatio = 0.5m,
            OverfittingScore = null,
            CreatedAt = DateTime.UtcNow
        };

        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(run);
        db.BacktestResults.Add(result);
        await db.SaveChangesAsync();

        var loaded = db.BacktestResults.Single(r => r.Id == result.Id);
        Assert.Null(loaded.OverfittingScore);
    }
}
