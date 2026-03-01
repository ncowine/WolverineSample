namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Computes comprehensive performance metrics from backtest results.
/// All methods are pure/static — no DB dependency.
/// Uses 252 trading days/year for annualization.
/// </summary>
public static class PerformanceCalculator
{
    private const int TradingDaysPerYear = 252;

    /// <summary>
    /// Compute all performance metrics from a backtest result.
    /// </summary>
    /// <param name="result">Backtest engine output (equity curve, trades).</param>
    /// <param name="riskFreeRate">Annual risk-free rate as % (e.g. 4.5).</param>
    /// <param name="benchmarkEquityCurve">Optional SPY equity curve for alpha/beta. Dates must overlap.</param>
    public static PerformanceMetrics Calculate(
        BacktestEngineResult result,
        decimal riskFreeRate = 4.5m,
        List<EquityPoint>? benchmarkEquityCurve = null)
    {
        var equityCurve = result.EquityCurve;
        var trades = result.Trades;

        var dailyReturns = ComputeDailyReturns(equityCurve);
        var totalYears = ComputeYears(result.StartDate, result.EndDate);
        var totalReturn = result.InitialCapital > 0
            ? (result.FinalEquity - result.InitialCapital) / result.InitialCapital * 100m
            : 0m;

        var cagr = ComputeCagr(result.InitialCapital, result.FinalEquity, totalYears);
        var volatility = ComputeAnnualizedVolatility(dailyReturns);
        var (maxDd, maxDdDuration) = ComputeMaxDrawdown(equityCurve);
        var sharpe = ComputeSharpe(dailyReturns, riskFreeRate);
        var sortino = ComputeSortino(dailyReturns, riskFreeRate);
        var calmar = maxDd != 0 ? cagr / Math.Abs(maxDd) : 0m;

        // Trade statistics
        var winners = trades.Where(t => t.PnL > 0).ToList();
        var losers = trades.Where(t => t.PnL <= 0).ToList();
        var grossProfit = winners.Sum(t => t.PnL);
        var grossLoss = Math.Abs(losers.Sum(t => t.PnL));
        var avgWin = winners.Count > 0 ? winners.Average(t => t.PnL) : 0m;
        var avgLoss = losers.Count > 0 ? losers.Average(t => t.PnL) : 0m;
        var profitFactor = grossLoss != 0 ? grossProfit / grossLoss : grossProfit > 0 ? decimal.MaxValue : 0m;
        var winRate = trades.Count > 0 ? (decimal)winners.Count / trades.Count * 100m : 0m;
        var expectancy = trades.Count > 0 ? trades.Average(t => t.PnL) : 0m;

        // Benchmark
        var benchmarkReturn = 0m;
        var benchmarkCagr = 0m;
        var alpha = 0m;
        var beta = 0m;
        if (benchmarkEquityCurve is { Count: >= 2 })
        {
            var benchStart = benchmarkEquityCurve[0].Value;
            var benchEnd = benchmarkEquityCurve[^1].Value;
            benchmarkReturn = benchStart > 0 ? (benchEnd - benchStart) / benchStart * 100m : 0m;
            benchmarkCagr = ComputeCagr(benchStart, benchEnd, totalYears);

            var benchDailyReturns = ComputeDailyReturns(benchmarkEquityCurve);
            (alpha, beta) = ComputeAlphaBeta(dailyReturns, benchDailyReturns, riskFreeRate, totalYears);
        }

        // Monthly/yearly returns
        var monthlyReturns = ComputeMonthlyReturns(equityCurve);
        var yearlyReturns = ComputeYearlyReturns(equityCurve);

        // Red flags
        var redFlags = new List<string>();
        if (sharpe < 1.0m)
            redFlags.Add($"Low Sharpe ratio ({sharpe:F2} < 1.0)");
        if (benchmarkEquityCurve is { Count: >= 2 } && cagr < benchmarkCagr)
            redFlags.Add($"Strategy CAGR ({cagr:F2}%) underperforms SPY ({benchmarkCagr:F2}%)");
        if (maxDd < -30m)
            redFlags.Add($"Severe max drawdown ({maxDd:F1}%)");
        if (profitFactor < 1m && trades.Count > 0)
            redFlags.Add($"Profit factor below 1.0 ({profitFactor:F2})");
        if (winRate < 30m && trades.Count > 0)
            redFlags.Add($"Very low win rate ({winRate:F1}%)");

        return new PerformanceMetrics
        {
            TotalReturn = totalReturn,
            Cagr = cagr,
            AnnualizedVolatility = volatility,
            SharpeRatio = sharpe,
            SortinoRatio = sortino,
            CalmarRatio = calmar,
            MaxDrawdownPercent = maxDd,
            MaxDrawdownDurationDays = maxDdDuration,
            TotalTrades = trades.Count,
            WinningTrades = winners.Count,
            LosingTrades = losers.Count,
            WinRate = winRate,
            ProfitFactor = profitFactor,
            Expectancy = expectancy,
            AverageWin = avgWin,
            AverageLoss = avgLoss,
            LargestWin = winners.Count > 0 ? winners.Max(t => t.PnL) : 0m,
            LargestLoss = losers.Count > 0 ? losers.Min(t => t.PnL) : 0m,
            AverageHoldingDays = trades.Count > 0 ? (decimal)trades.Average(t => t.HoldingDays) : 0m,
            BenchmarkReturn = benchmarkReturn,
            BenchmarkCagr = benchmarkCagr,
            Alpha = alpha,
            Beta = beta,
            MonthlyReturns = monthlyReturns,
            YearlyReturns = yearlyReturns,
            RedFlags = redFlags
        };
    }

    // ── Core Calculations ─────────────────────────────────────

    internal static decimal[] ComputeDailyReturns(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2) return Array.Empty<decimal>();

        var returns = new decimal[equityCurve.Count - 1];
        for (var i = 1; i < equityCurve.Count; i++)
        {
            var prev = equityCurve[i - 1].Value;
            returns[i - 1] = prev != 0 ? (equityCurve[i].Value - prev) / prev : 0m;
        }
        return returns;
    }

    internal static decimal ComputeCagr(decimal startValue, decimal endValue, decimal years)
    {
        if (startValue <= 0 || endValue <= 0 || years <= 0) return 0m;

        var ratio = (double)(endValue / startValue);
        var exponent = 1.0 / (double)years;
        var cagr = Math.Pow(ratio, exponent) - 1.0;
        return (decimal)cagr * 100m;
    }

    internal static decimal ComputeAnnualizedVolatility(decimal[] dailyReturns)
    {
        if (dailyReturns.Length < 2) return 0m;
        var stdDev = StdDev(dailyReturns);
        return stdDev * (decimal)Math.Sqrt(TradingDaysPerYear) * 100m;
    }

    internal static decimal ComputeSharpe(decimal[] dailyReturns, decimal riskFreeRateAnnual)
    {
        if (dailyReturns.Length < 2) return 0m;

        var rfDaily = riskFreeRateAnnual / 100m / TradingDaysPerYear;
        var excessReturns = dailyReturns.Select(r => r - rfDaily).ToArray();
        var meanExcess = excessReturns.Average();
        var stdDev = StdDev(excessReturns);

        if (stdDev == 0) return 0m;
        return meanExcess / stdDev * (decimal)Math.Sqrt(TradingDaysPerYear);
    }

    internal static decimal ComputeSortino(decimal[] dailyReturns, decimal riskFreeRateAnnual)
    {
        if (dailyReturns.Length < 2) return 0m;

        var rfDaily = riskFreeRateAnnual / 100m / TradingDaysPerYear;
        var excessReturns = dailyReturns.Select(r => r - rfDaily).ToArray();
        var meanExcess = excessReturns.Average();

        // Downside deviation: only negative excess returns
        var negativeReturns = excessReturns.Where(r => r < 0).ToArray();
        if (negativeReturns.Length == 0) return meanExcess > 0 ? decimal.MaxValue : 0m;

        var downsideDev = (decimal)Math.Sqrt((double)negativeReturns.Average(r => r * r));
        if (downsideDev == 0) return 0m;

        return meanExcess / downsideDev * (decimal)Math.Sqrt(TradingDaysPerYear);
    }

    internal static (decimal MaxDrawdownPercent, int MaxDurationDays) ComputeMaxDrawdown(
        List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2) return (0m, 0);

        var peak = equityCurve[0].Value;
        var peakDate = equityCurve[0].Date;
        var maxDd = 0m;
        var maxDdDuration = 0;
        var currentDdStartDate = equityCurve[0].Date;
        var inDrawdown = false;

        foreach (var point in equityCurve)
        {
            if (point.Value >= peak)
            {
                if (inDrawdown)
                {
                    var duration = (point.Date - currentDdStartDate).Days;
                    if (duration > maxDdDuration) maxDdDuration = duration;
                }
                peak = point.Value;
                peakDate = point.Date;
                inDrawdown = false;
            }
            else
            {
                if (!inDrawdown)
                {
                    currentDdStartDate = peakDate;
                    inDrawdown = true;
                }

                var dd = peak > 0 ? (point.Value - peak) / peak * 100m : 0m;
                if (dd < maxDd) maxDd = dd;
            }
        }

        // If still in drawdown at end
        if (inDrawdown)
        {
            var duration = (equityCurve[^1].Date - currentDdStartDate).Days;
            if (duration > maxDdDuration) maxDdDuration = duration;
        }

        return (maxDd, maxDdDuration);
    }

    internal static (decimal Alpha, decimal Beta) ComputeAlphaBeta(
        decimal[] strategyReturns,
        decimal[] benchmarkReturns,
        decimal riskFreeRateAnnual,
        decimal totalYears)
    {
        var n = Math.Min(strategyReturns.Length, benchmarkReturns.Length);
        if (n < 2) return (0m, 0m);

        var sReturns = strategyReturns[..n];
        var bReturns = benchmarkReturns[..n];

        var meanS = sReturns.Average();
        var meanB = bReturns.Average();

        // Beta = Cov(S, B) / Var(B)
        var covariance = 0m;
        var varianceB = 0m;
        for (var i = 0; i < n; i++)
        {
            var diffS = sReturns[i] - meanS;
            var diffB = bReturns[i] - meanB;
            covariance += diffS * diffB;
            varianceB += diffB * diffB;
        }
        covariance /= n;
        varianceB /= n;

        var beta = varianceB != 0 ? covariance / varianceB : 0m;

        // Alpha = annualized strategy return - (rf + beta * (annualized market return - rf))
        var rfAnnual = riskFreeRateAnnual / 100m;
        var annualizedStrategyReturn = meanS * TradingDaysPerYear;
        var annualizedBenchmarkReturn = meanB * TradingDaysPerYear;
        var alpha = annualizedStrategyReturn - (rfAnnual + beta * (annualizedBenchmarkReturn - rfAnnual));
        alpha *= 100m; // convert to percentage

        return (alpha, beta);
    }

    // ── Monthly/Yearly Returns ────────────────────────────────

    internal static Dictionary<string, decimal> ComputeMonthlyReturns(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2) return new();

        var monthly = new Dictionary<string, decimal>();
        var grouped = equityCurve.GroupBy(e => new { e.Date.Year, e.Date.Month });

        decimal? prevMonthEnd = null;
        foreach (var month in grouped.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
        {
            var monthEnd = month.Last().Value;
            var monthStart = prevMonthEnd ?? month.First().Value;

            var key = $"{month.Key.Year}-{month.Key.Month:D2}";
            monthly[key] = monthStart != 0 ? (monthEnd - monthStart) / monthStart * 100m : 0m;
            prevMonthEnd = monthEnd;
        }

        return monthly;
    }

    internal static Dictionary<int, decimal> ComputeYearlyReturns(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2) return new();

        var yearly = new Dictionary<int, decimal>();
        var grouped = equityCurve.GroupBy(e => e.Date.Year);

        decimal? prevYearEnd = null;
        foreach (var year in grouped.OrderBy(g => g.Key))
        {
            var yearEnd = year.Last().Value;
            var yearStart = prevYearEnd ?? year.First().Value;

            yearly[year.Key] = yearStart != 0 ? (yearEnd - yearStart) / yearStart * 100m : 0m;
            prevYearEnd = yearEnd;
        }

        return yearly;
    }

    // ── Helpers ───────────────────────────────────────────────

    private static decimal ComputeYears(DateTime start, DateTime end)
    {
        var days = (end - start).TotalDays;
        return (decimal)(days / 365.25);
    }

    private static decimal StdDev(decimal[] values)
    {
        if (values.Length < 2) return 0m;
        var mean = values.Average();
        var sumSqDiff = values.Sum(v => (v - mean) * (v - mean));
        return (decimal)Math.Sqrt((double)(sumSqDiff / (values.Length - 1))); // sample std dev
    }
}
