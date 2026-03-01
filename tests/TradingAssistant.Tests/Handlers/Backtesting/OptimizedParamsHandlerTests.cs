using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Handlers.Backtesting;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Tests.Helpers;

namespace TradingAssistant.Tests.Handlers.Backtesting;

public class OptimizedParamsHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────

    private static Strategy SeedStrategy(string name = "TestStrategy")
    {
        return new Strategy
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test strategy",
            IsActive = true
        };
    }

    private static WalkForwardResult MakeWalkForwardResult(
        decimal avgOosSharpe = 1.5m,
        decimal avgEfficiency = 0.7m,
        decimal avgOverfitting = 0.2m,
        OverfittingGrade grade = OverfittingGrade.Good,
        Dictionary<string, decimal>? parameters = null)
    {
        parameters ??= new Dictionary<string, decimal>
        {
            ["SmaPeriod"] = 50,
            ["RsiThreshold"] = 30,
            ["AtrMultiplier"] = 2.0m
        };

        return new WalkForwardResult
        {
            AverageOutOfSampleSharpe = avgOosSharpe,
            AverageEfficiency = avgEfficiency,
            AverageOverfittingScore = avgOverfitting,
            Grade = grade,
            BlessedParameters = new ParameterSet { Values = parameters },
            Windows = new List<WalkForwardWindow>
            {
                new()
                {
                    WindowNumber = 1,
                    InSampleSharpe = 2.0m,
                    OutOfSampleSharpe = avgOosSharpe,
                    OverfittingScore = avgOverfitting,
                    Efficiency = avgEfficiency
                }
            }
        };
    }

    // ── SaveOptimizedParams ──────────────────────────────────

    [Fact]
    public async Task Save_persists_blessed_params()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var wfResult = MakeWalkForwardResult();
        var dto = await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wfResult, db);

        Assert.Equal(strategy.Id, dto.StrategyId);
        Assert.Equal(1, dto.Version);
        Assert.True(dto.IsActive);
        Assert.Equal(50m, dto.Parameters["SmaPeriod"]);
        Assert.Equal(30m, dto.Parameters["RsiThreshold"]);
        Assert.Equal(1.5m, dto.AvgOutOfSampleSharpe);
        Assert.Equal(0.7m, dto.AvgEfficiency);
        Assert.Equal("Good", dto.OverfittingGrade);
        Assert.Equal(1, dto.WindowCount);
    }

    [Fact]
    public async Task Save_deactivates_previous_active_set()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var wf1 = MakeWalkForwardResult(avgOosSharpe: 1.0m);
        var dto1 = await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf1, db);

        var wf2 = MakeWalkForwardResult(avgOosSharpe: 1.8m);
        var dto2 = await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf2, db);

        // Reload from DB
        var all = db.OptimizedParameterSets
            .Where(p => p.StrategyId == strategy.Id)
            .OrderBy(p => p.Version)
            .ToList();

        Assert.Equal(2, all.Count);
        Assert.False(all[0].IsActive); // v1 deactivated
        Assert.True(all[1].IsActive);  // v2 is active
        Assert.Equal(1, all[0].Version);
        Assert.Equal(2, all[1].Version);
    }

    [Fact]
    public async Task Save_increments_version_numbers()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            var wf = MakeWalkForwardResult(avgOosSharpe: 1.0m + i * 0.5m);
            var dto = await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf, db);
            Assert.Equal(i + 1, dto.Version);
        }

        Assert.Equal(3, db.OptimizedParameterSets.Count(p => p.StrategyId == strategy.Id));
    }

    [Fact]
    public async Task Save_purges_beyond_max_versions()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        // Create 6 versions (limit is 5)
        for (var i = 0; i < 6; i++)
        {
            var wf = MakeWalkForwardResult(avgOosSharpe: 1.0m + i * 0.1m);
            await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf, db);
        }

        var remaining = db.OptimizedParameterSets
            .Where(p => p.StrategyId == strategy.Id)
            .OrderBy(p => p.Version)
            .ToList();

        // Should have exactly 5 versions
        Assert.Equal(5, remaining.Count);
        // Oldest (v1) should have been purged
        Assert.Equal(2, remaining[0].Version);
        // Latest (v6) should be active
        Assert.True(remaining[^1].IsActive);
        Assert.Equal(6, remaining[^1].Version);
    }

    [Fact]
    public async Task Save_different_strategies_independent()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var s1 = SeedStrategy("Strategy1");
        var s2 = SeedStrategy("Strategy2");
        db.Strategies.AddRange(s1, s2);
        await db.SaveChangesAsync();

        var wf1 = MakeWalkForwardResult(avgOosSharpe: 1.0m);
        var wf2 = MakeWalkForwardResult(avgOosSharpe: 2.0m);

        await SaveOptimizedParamsHandler.HandleAsync(s1.Id, wf1, db);
        await SaveOptimizedParamsHandler.HandleAsync(s2.Id, wf2, db);

        var s1Params = db.OptimizedParameterSets.Where(p => p.StrategyId == s1.Id).ToList();
        var s2Params = db.OptimizedParameterSets.Where(p => p.StrategyId == s2.Id).ToList();

        Assert.Single(s1Params);
        Assert.Single(s2Params);
        Assert.Equal(1.0m, s1Params[0].AvgOutOfSampleSharpe);
        Assert.Equal(2.0m, s2Params[0].AvgOutOfSampleSharpe);
    }

    // ── GetOptimizedParams ───────────────────────────────────

    [Fact]
    public async Task Get_returns_current_and_history()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        // Create 3 versions
        for (var i = 0; i < 3; i++)
        {
            var wf = MakeWalkForwardResult(avgOosSharpe: 1.0m + i * 0.5m);
            await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf, db);
        }

        var result = await GetOptimizedParamsHandler.HandleAsync(
            new GetOptimizedParamsQuery(strategy.Id), db);

        Assert.NotNull(result.Current);
        Assert.Equal(3, result.Current.Version);
        Assert.True(result.Current.IsActive);
        Assert.Equal(3, result.History.Count);
        // History ordered by version descending
        Assert.Equal(3, result.History[0].Version);
        Assert.Equal(2, result.History[1].Version);
        Assert.Equal(1, result.History[2].Version);
    }

    [Fact]
    public async Task Get_no_params_returns_null_current()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var result = await GetOptimizedParamsHandler.HandleAsync(
            new GetOptimizedParamsQuery(strategy.Id), db);

        Assert.Null(result.Current);
        Assert.Empty(result.History);
    }

    [Fact]
    public async Task Get_returns_walk_forward_metrics()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var wf = MakeWalkForwardResult(
            avgOosSharpe: 1.8m,
            avgEfficiency: 0.65m,
            avgOverfitting: 0.25m,
            grade: OverfittingGrade.Good);
        await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf, db);

        var result = await GetOptimizedParamsHandler.HandleAsync(
            new GetOptimizedParamsQuery(strategy.Id), db);

        Assert.Equal(1.8m, result.Current!.AvgOutOfSampleSharpe);
        Assert.Equal(0.65m, result.Current.AvgEfficiency);
        Assert.Equal(0.25m, result.Current.AvgOverfittingScore);
        Assert.Equal("Good", result.Current.OverfittingGrade);
    }

    // ── Screener integration ─────────────────────────────────

    [Fact]
    public async Task Active_params_deserialize_to_parameter_set()
    {
        using var db = TestBacktestDbContextFactory.Create();
        var strategy = SeedStrategy();
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var expectedParams = new Dictionary<string, decimal>
        {
            ["SmaPeriod"] = 20,
            ["EmaPeriod"] = 50,
            ["RsiThreshold"] = 25,
            ["AtrMultiplier"] = 1.5m
        };
        var wf = MakeWalkForwardResult(parameters: expectedParams);
        await SaveOptimizedParamsHandler.HandleAsync(strategy.Id, wf, db);

        var result = await GetOptimizedParamsHandler.HandleAsync(
            new GetOptimizedParamsQuery(strategy.Id), db);

        // Verify params round-trip correctly for screener consumption
        Assert.Equal(20m, result.Current!.Parameters["SmaPeriod"]);
        Assert.Equal(50m, result.Current.Parameters["EmaPeriod"]);
        Assert.Equal(25m, result.Current.Parameters["RsiThreshold"]);
        Assert.Equal(1.5m, result.Current.Parameters["AtrMultiplier"]);
    }
}
