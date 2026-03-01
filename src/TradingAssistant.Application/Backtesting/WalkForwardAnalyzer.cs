using TradingAssistant.Application.Indicators;

namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Walk-forward analysis: split data into windows, optimize on in-sample,
/// validate on out-of-sample, detect overfitting.
/// </summary>
public static class WalkForwardAnalyzer
{
    /// <summary>
    /// Run walk-forward analysis.
    /// </summary>
    /// <param name="bars">Full aligned daily bar array (with pre-computed indicators).</param>
    /// <param name="parameterSpace">Parameters to optimize.</param>
    /// <param name="backtestRunnerFactory">
    /// Given a ParameterSet and a bar slice, run a backtest and return the result.
    /// The caller applies params to strategy/indicators and runs BacktestEngine.
    /// </param>
    /// <param name="config">Walk-forward configuration.</param>
    public static WalkForwardResult Analyze(
        CandleWithIndicators[] bars,
        ParameterSpace parameterSpace,
        Func<ParameterSet, CandleWithIndicators[], BacktestEngineResult> backtestRunnerFactory,
        WalkForwardConfig? config = null)
    {
        config ??= new WalkForwardConfig();
        var startTime = DateTime.UtcNow;
        var warnings = new List<string>();

        var windows = GenerateWindows(bars, config);
        if (windows.Count == 0)
        {
            warnings.Add("Insufficient data for walk-forward analysis");
            return new WalkForwardResult
            {
                Config = config,
                Warnings = warnings,
                ElapsedTime = DateTime.UtcNow - startTime
            };
        }

        var wfWindows = new List<WalkForwardWindow>();
        var aggregatedEquity = new List<EquityPoint>();

        for (var i = 0; i < windows.Count; i++)
        {
            var (isStart, isEnd, oosStart, oosEnd) = windows[i];

            // Slice bars for in-sample and out-of-sample
            var inSampleBars = SliceBars(bars, isStart, isEnd);
            var oosBars = SliceBars(bars, oosStart, oosEnd);

            if (inSampleBars.Length < 2 || oosBars.Length < 2)
            {
                warnings.Add($"Window {i + 1}: insufficient bars (IS={inSampleBars.Length}, OOS={oosBars.Length})");
                continue;
            }

            // Optimize on in-sample
            var optimizationResult = GridSearchOptimizer.RunSequential(
                parameterSpace,
                paramSet => backtestRunnerFactory(paramSet, inSampleBars),
                config.RiskFreeRate,
                topN: config.OptimizationTopN);

            if (optimizationResult.TopResults.Count == 0)
            {
                warnings.Add($"Window {i + 1}: optimization produced no results");
                continue;
            }

            var bestTrial = optimizationResult.TopResults[0];
            var bestParams = bestTrial.Parameters;
            var isSharpe = bestTrial.Metrics.SharpeRatio;

            // Test best params on out-of-sample
            var oosResult = backtestRunnerFactory(bestParams, oosBars);
            var oosMetrics = PerformanceCalculator.Calculate(oosResult, config.RiskFreeRate);
            var oosSharpe = oosMetrics.SharpeRatio;

            // Compute overfitting metrics
            var overfittingScore = isSharpe != 0
                ? (isSharpe - oosSharpe) / Math.Abs(isSharpe)
                : 0m;
            var efficiency = isSharpe != 0
                ? oosSharpe / Math.Abs(isSharpe)
                : 0m;

            wfWindows.Add(new WalkForwardWindow
            {
                WindowNumber = i + 1,
                InSampleStart = inSampleBars[0].Timestamp,
                InSampleEnd = inSampleBars[^1].Timestamp,
                OutOfSampleStart = oosBars[0].Timestamp,
                OutOfSampleEnd = oosBars[^1].Timestamp,
                BestParameters = bestParams,
                InSampleSharpe = isSharpe,
                OutOfSampleSharpe = oosSharpe,
                OutOfSampleMetrics = oosMetrics,
                OverfittingScore = overfittingScore,
                Efficiency = efficiency
            });

            // Aggregate OOS equity curve
            aggregatedEquity.AddRange(oosResult.EquityCurve);
        }

        // Compute aggregates
        var avgIsSharpe = wfWindows.Count > 0 ? wfWindows.Average(w => w.InSampleSharpe) : 0m;
        var avgOosSharpe = wfWindows.Count > 0 ? wfWindows.Average(w => w.OutOfSampleSharpe) : 0m;
        var avgOverfitting = wfWindows.Count > 0 ? wfWindows.Average(w => w.OverfittingScore) : 0m;
        var avgEfficiency = wfWindows.Count > 0 ? wfWindows.Average(w => w.Efficiency) : 0m;

        // Grade
        var grade = avgOverfitting switch
        {
            < 0.30m => OverfittingGrade.Good,
            < 0.50m => OverfittingGrade.Warning,
            _ => OverfittingGrade.Overfitted
        };

        // Blessed params: from window with best OOS Sharpe
        var blessedParams = wfWindows.Count > 0
            ? wfWindows.OrderByDescending(w => w.OutOfSampleSharpe).First().BestParameters
            : new ParameterSet();

        if (avgEfficiency < 0.5m && wfWindows.Count > 0)
            warnings.Add($"Low walk-forward efficiency ({avgEfficiency:F2}): likely overfitted");

        return new WalkForwardResult
        {
            Config = config,
            Windows = wfWindows,
            AverageInSampleSharpe = avgIsSharpe,
            AverageOutOfSampleSharpe = avgOosSharpe,
            AverageOverfittingScore = avgOverfitting,
            AverageEfficiency = avgEfficiency,
            Grade = grade,
            BlessedParameters = blessedParams,
            AggregatedEquityCurve = aggregatedEquity,
            ElapsedTime = DateTime.UtcNow - startTime,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Generate walk-forward window date ranges.
    /// </summary>
    internal static List<(int IsStart, int IsEnd, int OosStart, int OosEnd)> GenerateWindows(
        CandleWithIndicators[] bars,
        WalkForwardConfig config)
    {
        var totalBars = bars.Length;
        var windows = new List<(int, int, int, int)>();
        var minBarsNeeded = config.InSampleDays + config.OutOfSampleDays;

        if (totalBars < minBarsNeeded)
            return windows;

        if (config.Mode == WalkForwardMode.Rolling)
        {
            var start = 0;
            while (start + minBarsNeeded <= totalBars)
            {
                var isStart = start;
                var isEnd = start + config.InSampleDays - 1;
                var oosStart = isEnd + 1;
                var oosEnd = Math.Min(oosStart + config.OutOfSampleDays - 1, totalBars - 1);

                windows.Add((isStart, isEnd, oosStart, oosEnd));
                start += config.OutOfSampleDays; // slide by OOS size
            }
        }
        else // Anchored
        {
            var oosStart = config.InSampleDays;
            while (oosStart + config.OutOfSampleDays <= totalBars)
            {
                var isStart = 0; // always from the beginning
                var isEnd = oosStart - 1;
                var oosEnd = Math.Min(oosStart + config.OutOfSampleDays - 1, totalBars - 1);

                windows.Add((isStart, isEnd, oosStart, oosEnd));
                oosStart += config.OutOfSampleDays;
            }
        }

        return windows;
    }

    private static CandleWithIndicators[] SliceBars(CandleWithIndicators[] bars, int start, int end)
    {
        var length = Math.Min(end - start + 1, bars.Length - start);
        if (length <= 0) return Array.Empty<CandleWithIndicators>();
        return bars[start..(start + length)];
    }
}
