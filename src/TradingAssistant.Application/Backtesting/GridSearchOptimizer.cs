namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// One trial result: parameter values + computed metrics.
/// </summary>
public class OptimizationTrial
{
    public ParameterSet Parameters { get; init; } = new();
    public PerformanceMetrics Metrics { get; init; } = new();
    public decimal RankingScore { get; init; }
}

/// <summary>
/// Complete grid search result.
/// </summary>
public class GridSearchResult
{
    public long TotalCombinations { get; init; }
    public long CompletedCombinations { get; init; }
    public List<OptimizationTrial> TopResults { get; init; } = new();
    public TimeSpan ElapsedTime { get; init; }
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Progress callback for tracking grid search execution.
/// </summary>
public class GridSearchProgress
{
    public long Completed { get; set; }
    public long Total { get; set; }
    public decimal PercentComplete => Total > 0 ? (decimal)Completed / Total * 100m : 0;
    public decimal BestSharpe { get; set; }
}

/// <summary>
/// Grid search optimizer: enumerates all parameter combinations, runs a backtest for each,
/// ranks by Sharpe Ratio, and returns the top N results.
/// </summary>
public static class GridSearchOptimizer
{
    /// <summary>
    /// Run a grid search optimization.
    /// </summary>
    /// <param name="space">Parameter space to search.</param>
    /// <param name="backtestRunner">
    /// Delegate that runs a single backtest for a given parameter set.
    /// The caller is responsible for applying parameters to IndicatorConfig/StrategyDefinition,
    /// computing indicators, and running BacktestEngine.
    /// </param>
    /// <param name="riskFreeRate">Annual risk-free rate for Sharpe/Sortino computation.</param>
    /// <param name="topN">Number of top results to keep.</param>
    /// <param name="onProgress">Optional progress callback invoked after each trial.</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    public static GridSearchResult Run(
        ParameterSpace space,
        Func<ParameterSet, BacktestEngineResult> backtestRunner,
        decimal riskFreeRate = 4.5m,
        int topN = 10,
        Action<GridSearchProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var totalCombinations = space.TotalCombinations;

        if (totalCombinations == 0)
        {
            return new GridSearchResult
            {
                TotalCombinations = 0,
                Warnings = new List<string> { "No parameter combinations to test" }
            };
        }

        if (space.IsLarge)
            warnings.Add($"Large parameter space: {totalCombinations:N0} combinations (consider reducing ranges)");

        var startTime = DateTime.UtcNow;
        var progress = new GridSearchProgress { Total = totalCombinations };

        // Thread-safe collection for top results
        var allTrials = new List<OptimizationTrial>();
        var lockObj = new object();

        // Run all combinations in parallel
        var combinations = ParameterGrid.Enumerate(space).ToList();

        Parallel.ForEach(combinations,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            },
            paramSet =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = backtestRunner(paramSet);
                    var metrics = PerformanceCalculator.Calculate(result, riskFreeRate);

                    var trial = new OptimizationTrial
                    {
                        Parameters = paramSet,
                        Metrics = metrics,
                        RankingScore = metrics.SharpeRatio
                    };

                    lock (lockObj)
                    {
                        allTrials.Add(trial);
                        progress.Completed++;
                        if (metrics.SharpeRatio > progress.BestSharpe)
                            progress.BestSharpe = metrics.SharpeRatio;
                    }

                    onProgress?.Invoke(progress);
                }
                catch (Exception)
                {
                    lock (lockObj)
                    {
                        progress.Completed++;
                    }
                }
            });

        var topResults = allTrials
            .OrderByDescending(t => t.RankingScore)
            .Take(topN)
            .ToList();

        return new GridSearchResult
        {
            TotalCombinations = totalCombinations,
            CompletedCombinations = progress.Completed,
            TopResults = topResults,
            ElapsedTime = DateTime.UtcNow - startTime,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Sequential run for simpler use cases or testing.
    /// </summary>
    public static GridSearchResult RunSequential(
        ParameterSpace space,
        Func<ParameterSet, BacktestEngineResult> backtestRunner,
        decimal riskFreeRate = 4.5m,
        int topN = 10,
        Action<GridSearchProgress>? onProgress = null)
    {
        var warnings = new List<string>();
        var totalCombinations = space.TotalCombinations;

        if (totalCombinations == 0)
        {
            return new GridSearchResult
            {
                TotalCombinations = 0,
                Warnings = new List<string> { "No parameter combinations to test" }
            };
        }

        if (space.IsLarge)
            warnings.Add($"Large parameter space: {totalCombinations:N0} combinations (consider reducing ranges)");

        var startTime = DateTime.UtcNow;
        var progress = new GridSearchProgress { Total = totalCombinations };
        var allTrials = new List<OptimizationTrial>();

        foreach (var paramSet in ParameterGrid.Enumerate(space))
        {
            try
            {
                var result = backtestRunner(paramSet);
                var metrics = PerformanceCalculator.Calculate(result, riskFreeRate);

                allTrials.Add(new OptimizationTrial
                {
                    Parameters = paramSet,
                    Metrics = metrics,
                    RankingScore = metrics.SharpeRatio
                });

                progress.Completed++;
                if (metrics.SharpeRatio > progress.BestSharpe)
                    progress.BestSharpe = metrics.SharpeRatio;

                onProgress?.Invoke(progress);
            }
            catch (Exception)
            {
                progress.Completed++;
            }
        }

        var topResults = allTrials
            .OrderByDescending(t => t.RankingScore)
            .Take(topN)
            .ToList();

        return new GridSearchResult
        {
            TotalCombinations = totalCombinations,
            CompletedCombinations = progress.Completed,
            TopResults = topResults,
            ElapsedTime = DateTime.UtcNow - startTime,
            Warnings = warnings
        };
    }
}
