using TradingAssistant.Application.Handlers.Backtesting;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Backtesting;

public class CompareBacktestsHandlerTests
{
    private static (Strategy strategy, BacktestRun run, BacktestResult result) SeedBacktest(
        string symbol, decimal totalReturn, decimal sharpe, decimal cagr)
    {
        var strategy = new Strategy
        {
            Id = Guid.NewGuid(),
            Name = $"Strategy-{symbol}",
            CreatedAt = DateTime.UtcNow
        };

        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            StrategyId = strategy.Id,
            Symbol = symbol,
            StartDate = new DateTime(2020, 1, 1),
            EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        var result = new BacktestResult
        {
            Id = Guid.NewGuid(),
            BacktestRunId = run.Id,
            TotalTrades = 100,
            WinRate = 55m,
            TotalReturn = totalReturn,
            MaxDrawdown = 12m,
            SharpeRatio = sharpe,
            Cagr = cagr,
            SortinoRatio = sharpe * 1.2m,
            CalmarRatio = cagr / 12m,
            ProfitFactor = 1.8m,
            Expectancy = 50m,
            OverfittingScore = 0.25m,
            ParametersJson = "{\"SmaShort\":10,\"SmaLong\":50}",
            SpyComparisonJson = "{\"StrategyCagr\":15,\"SpyCagr\":10,\"Alpha\":5,\"Beta\":0.8}",
            CreatedAt = DateTime.UtcNow
        };

        return (strategy, run, result);
    }

    [Fact]
    public async Task Compare_returns_entries_for_valid_ids()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var (s1, r1, res1) = SeedBacktest("AAPL", 50m, 1.5m, 10m);
        var (s2, r2, res2) = SeedBacktest("MSFT", 30m, 1.2m, 7m);

        db.Strategies.AddRange(s1, s2);
        db.BacktestRuns.AddRange(r1, r2);
        db.BacktestResults.AddRange(res1, res2);
        await db.SaveChangesAsync();

        var query = new CompareBacktestsQuery(new List<Guid> { r1.Id, r2.Id });
        var result = await CompareBacktestsHandler.HandleAsync(query, db);

        Assert.Equal(2, result.Entries.Count);
        Assert.Contains(result.Entries, e => e.Symbol == "AAPL");
        Assert.Contains(result.Entries, e => e.Symbol == "MSFT");
    }

    [Fact]
    public async Task Compare_maps_all_metrics()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var (strategy, run, backtestResult) = SeedBacktest("AAPL", 50m, 1.5m, 10m);

        db.Strategies.Add(strategy);
        db.BacktestRuns.Add(run);
        db.BacktestResults.Add(backtestResult);
        await db.SaveChangesAsync();

        var query = new CompareBacktestsQuery(new List<Guid> { run.Id });
        var result = await CompareBacktestsHandler.HandleAsync(query, db);

        var entry = result.Entries.Single();
        Assert.Equal(run.Id, entry.BacktestRunId);
        Assert.Equal("AAPL", entry.Symbol);
        Assert.Equal("Strategy-AAPL", entry.StrategyName);
        Assert.Equal(100, entry.TotalTrades);
        Assert.Equal(55m, entry.WinRate);
        Assert.Equal(50m, entry.TotalReturn);
        Assert.Equal(10m, entry.Cagr);
        Assert.Equal(1.5m, entry.SharpeRatio);
        Assert.Equal(1.8m, entry.SortinoRatio);
        Assert.Equal(12m, entry.MaxDrawdown);
        Assert.Equal(1.8m, entry.ProfitFactor);
        Assert.Equal(50m, entry.Expectancy);
        Assert.Equal(0.25m, entry.OverfittingScore);
        Assert.NotNull(entry.ParametersJson);
        Assert.NotNull(entry.SpyComparisonJson);
    }

    [Fact]
    public async Task Compare_empty_ids_returns_empty()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var query = new CompareBacktestsQuery(new List<Guid>());
        var result = await CompareBacktestsHandler.HandleAsync(query, db);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task Compare_nonexistent_ids_returns_empty()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var query = new CompareBacktestsQuery(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        var result = await CompareBacktestsHandler.HandleAsync(query, db);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task Compare_skips_runs_without_results()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var (strategy, run, backtestResult) = SeedBacktest("AAPL", 50m, 1.5m, 10m);

        // Create a second run WITHOUT a result
        var runNoResult = new BacktestRun
        {
            Id = Guid.NewGuid(),
            StrategyId = strategy.Id,
            Symbol = "GOOG",
            StartDate = new DateTime(2020, 1, 1),
            EndDate = new DateTime(2025, 1, 1),
            Status = BacktestRunStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Strategies.Add(strategy);
        db.BacktestRuns.AddRange(run, runNoResult);
        db.BacktestResults.Add(backtestResult);
        await db.SaveChangesAsync();

        var query = new CompareBacktestsQuery(new List<Guid> { run.Id, runNoResult.Id });
        var result = await CompareBacktestsHandler.HandleAsync(query, db);

        Assert.Single(result.Entries);
        Assert.Equal("AAPL", result.Entries[0].Symbol);
    }

    [Fact]
    public async Task Compare_rejects_more_than_10()
    {
        await using var db = TestBacktestDbContextFactory.Create();

        var ids = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();
        var query = new CompareBacktestsQuery(ids);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CompareBacktestsHandler.HandleAsync(query, db));
    }
}
