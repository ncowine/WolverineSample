using TradingAssistant.Application.Backtesting;

namespace TradingAssistant.Tests.Backtesting;

public class GridSearchOptimizerTests
{
    // ── ParameterDefinition ───────────────────────────────────

    [Fact]
    public void Parameter_value_count_computed()
    {
        var param = new ParameterDefinition { Name = "RSI", Min = 10, Max = 30, Step = 5 };
        // Values: 10, 15, 20, 25, 30 → 5
        Assert.Equal(5, param.ValueCount);
    }

    [Fact]
    public void Parameter_enumerates_values()
    {
        var param = new ParameterDefinition { Name = "SMA", Min = 5, Max = 20, Step = 5 };
        var values = param.EnumerateValues().ToList();

        Assert.Equal(new[] { 5m, 10m, 15m, 20m }, values);
    }

    [Fact]
    public void Parameter_single_value_when_min_equals_max()
    {
        var param = new ParameterDefinition { Name = "X", Min = 10, Max = 10, Step = 1 };
        Assert.Equal(1, param.ValueCount);
        Assert.Single(param.EnumerateValues());
    }

    // ── ParameterSpace ────────────────────────────────────────

    [Fact]
    public void Total_combinations_computed()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 1, Max = 3, Step = 1 },  // 3 values
                new() { Name = "B", Min = 10, Max = 20, Step = 5 }, // 3 values
            }
        };

        Assert.Equal(9, space.TotalCombinations); // 3 * 3
        Assert.False(space.IsLarge);
    }

    [Fact]
    public void IsLarge_true_when_over_10000()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 1, Max = 100, Step = 1 },  // 100 values
                new() { Name = "B", Min = 1, Max = 200, Step = 1 },  // 200 values
            }
        };

        Assert.True(space.IsLarge);
        Assert.Equal(20_000, space.TotalCombinations);
    }

    // ── ParameterGrid ─────────────────────────────────────────

    [Fact]
    public void Grid_enumerates_all_combinations()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "X", Min = 1, Max = 2, Step = 1 },
                new() { Name = "Y", Min = 10, Max = 20, Step = 10 },
            }
        };

        var combos = ParameterGrid.Enumerate(space).ToList();

        Assert.Equal(4, combos.Count); // 2 * 2
        Assert.Contains(combos, c => c["X"] == 1 && c["Y"] == 10);
        Assert.Contains(combos, c => c["X"] == 1 && c["Y"] == 20);
        Assert.Contains(combos, c => c["X"] == 2 && c["Y"] == 10);
        Assert.Contains(combos, c => c["X"] == 2 && c["Y"] == 20);
    }

    [Fact]
    public void Grid_empty_for_no_parameters()
    {
        var space = new ParameterSpace { Parameters = new() };
        Assert.Empty(ParameterGrid.Enumerate(space));
    }

    [Fact]
    public void Grid_three_dimensions()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 1, Max = 2, Step = 1 },  // 2
                new() { Name = "B", Min = 1, Max = 3, Step = 1 },  // 3
                new() { Name = "C", Min = 10, Max = 20, Step = 10 }, // 2
            }
        };

        var combos = ParameterGrid.Enumerate(space).ToList();
        Assert.Equal(12, combos.Count); // 2 * 3 * 2
    }

    // ── GridSearchOptimizer ───────────────────────────────────

    /// <summary>
    /// Fake backtest runner: returns a result whose "quality" depends on parameter values.
    /// Higher param A → higher final equity → higher Sharpe.
    /// </summary>
    private static BacktestEngineResult FakeBacktestRunner(ParameterSet paramSet)
    {
        var a = paramSet["A"];
        var multiplier = 1m + a / 100m; // A=10 → 1.10, A=20 → 1.20
        var initial = 100_000m;
        var final_ = initial * multiplier;

        // Build a simple equity curve with some noise for valid Sharpe computation
        var curve = new List<EquityPoint>();
        var start = new DateTime(2024, 1, 2);
        var days = 252;
        var noise = (final_ - initial) * 0.002m;

        for (var i = 0; i < days; i++)
        {
            var t = (decimal)i / (days - 1);
            var baseValue = initial + (final_ - initial) * t;
            var wiggle = (i % 2 == 0) ? noise : -noise;
            curve.Add(new EquityPoint(start.AddDays(i), baseValue + wiggle));
        }

        return new BacktestEngineResult
        {
            Symbol = "TEST",
            StartDate = start,
            EndDate = start.AddDays(days - 1),
            InitialCapital = initial,
            FinalEquity = final_,
            EquityCurve = curve,
            Trades = new List<TradeRecord>
            {
                new() { PnL = final_ - initial, HoldingDays = days }
            },
            Log = new()
        };
    }

    [Fact]
    public void Sequential_search_returns_ranked_results()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 5, Max = 25, Step = 5 }, // 5 values: 5,10,15,20,25
            }
        };

        var result = GridSearchOptimizer.RunSequential(space, FakeBacktestRunner, topN: 3);

        Assert.Equal(5, result.TotalCombinations);
        Assert.Equal(5, result.CompletedCombinations);
        Assert.Equal(3, result.TopResults.Count);

        // Top result should have highest A (25) → highest Sharpe
        Assert.Equal(25m, result.TopResults[0].Parameters["A"]);
        // Results should be in descending Sharpe order
        Assert.True(result.TopResults[0].RankingScore >= result.TopResults[1].RankingScore);
        Assert.True(result.TopResults[1].RankingScore >= result.TopResults[2].RankingScore);
    }

    [Fact]
    public void Parallel_search_returns_same_count()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 5, Max = 25, Step = 5 },
            }
        };

        var result = GridSearchOptimizer.Run(space, FakeBacktestRunner, topN: 5);

        Assert.Equal(5, result.TotalCombinations);
        Assert.Equal(5, result.CompletedCombinations);
        Assert.Equal(5, result.TopResults.Count);
    }

    [Fact]
    public void Progress_callback_invoked()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 10, Max = 30, Step = 10 }, // 3 values
            }
        };

        var progressUpdates = new List<GridSearchProgress>();
        GridSearchOptimizer.RunSequential(space, FakeBacktestRunner,
            onProgress: p => progressUpdates.Add(new GridSearchProgress
            {
                Completed = p.Completed,
                Total = p.Total,
                BestSharpe = p.BestSharpe
            }));

        Assert.Equal(3, progressUpdates.Count);
        Assert.Equal(1, progressUpdates[0].Completed);
        Assert.Equal(2, progressUpdates[1].Completed);
        Assert.Equal(3, progressUpdates[2].Completed);
        Assert.All(progressUpdates, p => Assert.Equal(3, p.Total));
    }

    [Fact]
    public void Large_space_generates_warning()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 1, Max = 100, Step = 1 },  // 100
                new() { Name = "B", Min = 1, Max = 200, Step = 1 },  // 200
            }
        };

        // Don't actually run 20k combinations — just check the warning logic
        // by using a fast stub
        var called = 0;
        var result = GridSearchOptimizer.RunSequential(space, _ =>
        {
            Interlocked.Increment(ref called);
            return new BacktestEngineResult
            {
                InitialCapital = 100_000m,
                FinalEquity = 100_000m,
                EquityCurve = new List<EquityPoint>
                {
                    new(new DateTime(2024, 1, 2), 100_000m),
                    new(new DateTime(2024, 1, 3), 100_000m),
                },
                Trades = new(), Log = new()
            };
        });

        Assert.Contains(result.Warnings, w => w.Contains("Large parameter space"));
        Assert.Equal(20_000, result.TotalCombinations);
    }

    [Fact]
    public void Empty_space_returns_empty_result()
    {
        var space = new ParameterSpace { Parameters = new() };
        var result = GridSearchOptimizer.RunSequential(space, _ => throw new Exception("should not be called"));

        Assert.Equal(0, result.TotalCombinations);
        Assert.Empty(result.TopResults);
    }

    [Fact]
    public void TopN_limits_results()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 1, Max = 10, Step = 1 }, // 10 values
            }
        };

        var result = GridSearchOptimizer.RunSequential(space, FakeBacktestRunner, topN: 3);

        Assert.Equal(10, result.CompletedCombinations);
        Assert.Equal(3, result.TopResults.Count);
    }

    [Fact]
    public void Multi_dimensional_search_works()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 10, Max = 20, Step = 5 }, // 3 values
                new() { Name = "B", Min = 1, Max = 3, Step = 1 },   // 3 values (ignored by runner)
            }
        };

        var result = GridSearchOptimizer.RunSequential(space, FakeBacktestRunner, topN: 5);

        Assert.Equal(9, result.TotalCombinations); // 3 * 3
        Assert.Equal(9, result.CompletedCombinations);
        Assert.Equal(5, result.TopResults.Count);

        // Best should have highest A regardless of B
        Assert.Equal(20m, result.TopResults[0].Parameters["A"]);
    }

    [Fact]
    public void Elapsed_time_tracked()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 10, Max = 20, Step = 10 },
            }
        };

        var result = GridSearchOptimizer.RunSequential(space, FakeBacktestRunner);

        Assert.True(result.ElapsedTime >= TimeSpan.Zero);
    }

    [Fact]
    public void Results_contain_full_metrics()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 20, Max = 20, Step = 1 }, // single value
            }
        };

        var result = GridSearchOptimizer.RunSequential(space, FakeBacktestRunner, topN: 1);

        Assert.Single(result.TopResults);
        var trial = result.TopResults[0];
        Assert.Equal(20m, trial.Parameters["A"]);
        Assert.True(trial.Metrics.TotalReturn > 0);
        Assert.True(trial.Metrics.Cagr > 0);
        Assert.Equal(1, trial.Metrics.TotalTrades);
    }

    [Fact]
    public void Failed_backtests_skipped_gracefully()
    {
        var space = new ParameterSpace
        {
            Parameters = new List<ParameterDefinition>
            {
                new() { Name = "A", Min = 1, Max = 3, Step = 1 },
            }
        };

        var callCount = 0;
        var result = GridSearchOptimizer.RunSequential(space, paramSet =>
        {
            callCount++;
            if (paramSet["A"] == 2)
                throw new InvalidOperationException("Simulated failure");
            return FakeBacktestRunner(paramSet);
        });

        Assert.Equal(3, result.CompletedCombinations); // all 3 counted
        Assert.Equal(2, result.TopResults.Count); // only 2 succeeded
    }
}
