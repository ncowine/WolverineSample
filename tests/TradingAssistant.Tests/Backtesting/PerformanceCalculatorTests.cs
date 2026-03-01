using TradingAssistant.Application.Backtesting;

namespace TradingAssistant.Tests.Backtesting;

public class PerformanceCalculatorTests
{
    // ── Helper Builders ───────────────────────────────────────

    /// <summary>
    /// Build an equity curve with a deterministic zigzag pattern to ensure non-zero variance.
    /// Even days get a small bump up, odd days get a small dip, but the overall trend
    /// follows a smooth path from startValue to endValue.
    /// </summary>
    private static List<EquityPoint> MakeEquityCurve(decimal startValue, decimal endValue, int days)
    {
        var curve = new List<EquityPoint>();
        var start = new DateTime(2024, 1, 2);
        var noise = (endValue - startValue) * 0.002m; // 0.2% of total range as noise

        for (var i = 0; i < days; i++)
        {
            var t = days > 1 ? (decimal)i / (days - 1) : 0m;
            var baseValue = startValue + (endValue - startValue) * t;
            var wiggle = (i % 2 == 0) ? noise : -noise;
            var value = baseValue + wiggle;
            curve.Add(new EquityPoint(start.AddDays(i), value));
        }
        return curve;
    }

    private static List<EquityPoint> MakeEquityCurveFromValues(params (int DayOffset, decimal Value)[] points)
    {
        var start = new DateTime(2024, 1, 2);
        return points.Select(p => new EquityPoint(start.AddDays(p.DayOffset), p.Value)).ToList();
    }

    private static BacktestEngineResult MakeResult(
        List<EquityPoint> equityCurve,
        List<TradeRecord>? trades = null)
    {
        return new BacktestEngineResult
        {
            Symbol = "TEST",
            StartDate = equityCurve.Count > 0 ? equityCurve[0].Date : DateTime.MinValue,
            EndDate = equityCurve.Count > 0 ? equityCurve[^1].Date : DateTime.MinValue,
            InitialCapital = equityCurve.Count > 0 ? equityCurve[0].Value : 100_000m,
            FinalEquity = equityCurve.Count > 0 ? equityCurve[^1].Value : 100_000m,
            Trades = trades ?? new(),
            EquityCurve = equityCurve,
            Log = new()
        };
    }

    // ── CAGR ──────────────────────────────────────────────────

    [Fact]
    public void Cagr_computed_correctly()
    {
        // $100k → $121k over 2 years = 10% CAGR (sqrt(1.21) - 1 = 0.10)
        var cagr = PerformanceCalculator.ComputeCagr(100_000m, 121_000m, 2m);
        Assert.InRange(cagr, 9.9m, 10.1m);
    }

    [Fact]
    public void Cagr_zero_for_zero_years()
    {
        Assert.Equal(0m, PerformanceCalculator.ComputeCagr(100_000m, 110_000m, 0m));
    }

    [Fact]
    public void Cagr_negative_for_loss()
    {
        // $100k → $90k over 1 year = -10%
        var cagr = PerformanceCalculator.ComputeCagr(100_000m, 90_000m, 1m);
        Assert.InRange(cagr, -10.1m, -9.9m);
    }

    // ── Daily Returns ─────────────────────────────────────────

    [Fact]
    public void Daily_returns_computed_from_equity_curve()
    {
        var curve = MakeEquityCurveFromValues(
            (0, 100m), (1, 105m), (2, 110m), (3, 100m));

        var returns = PerformanceCalculator.ComputeDailyReturns(curve);

        Assert.Equal(3, returns.Length);
        Assert.Equal(0.05m, returns[0]);  // 100 → 105 = +5%
        Assert.InRange(returns[1], 0.047m, 0.048m); // 105 → 110
        Assert.InRange(returns[2], -0.091m, -0.090m); // 110 → 100
    }

    [Fact]
    public void Daily_returns_empty_for_single_point()
    {
        var curve = MakeEquityCurveFromValues((0, 100m));
        Assert.Empty(PerformanceCalculator.ComputeDailyReturns(curve));
    }

    // ── Sharpe Ratio ──────────────────────────────────────────

    [Fact]
    public void Sharpe_positive_for_profitable_strategy()
    {
        // Steady upward equity curve → positive Sharpe
        var curve = MakeEquityCurve(100_000m, 120_000m, 252);
        var returns = PerformanceCalculator.ComputeDailyReturns(curve);
        var sharpe = PerformanceCalculator.ComputeSharpe(returns, 4.5m);

        Assert.True(sharpe > 0, $"Sharpe should be positive, got {sharpe}");
    }

    [Fact]
    public void Sharpe_zero_for_empty_returns()
    {
        Assert.Equal(0m, PerformanceCalculator.ComputeSharpe(Array.Empty<decimal>(), 4.5m));
    }

    // ── Sortino Ratio ─────────────────────────────────────────

    [Fact]
    public void Sortino_higher_than_sharpe_for_uptrend()
    {
        // In an uptrend with few down days, Sortino should be >= Sharpe
        // because downside deviation is smaller than total deviation
        var curve = MakeEquityCurve(100_000m, 130_000m, 252);
        var returns = PerformanceCalculator.ComputeDailyReturns(curve);

        var sharpe = PerformanceCalculator.ComputeSharpe(returns, 4.5m);
        var sortino = PerformanceCalculator.ComputeSortino(returns, 4.5m);

        // A perfectly smooth uptrend has no negative returns → Sortino = MaxValue
        // For a nearly smooth curve, Sortino should be very high
        Assert.True(sortino >= sharpe, $"Sortino ({sortino}) should be >= Sharpe ({sharpe})");
    }

    // ── Max Drawdown ──────────────────────────────────────────

    [Fact]
    public void Max_drawdown_computed_correctly()
    {
        var curve = MakeEquityCurveFromValues(
            (0, 100m), (1, 110m), (2, 90m), (3, 95m), (4, 105m));

        var (maxDd, _) = PerformanceCalculator.ComputeMaxDrawdown(curve);

        // Peak = 110, trough = 90 → drawdown = (90-110)/110 = -18.18%
        Assert.InRange(maxDd, -18.2m, -18.1m);
    }

    [Fact]
    public void Max_drawdown_duration_tracked()
    {
        var curve = MakeEquityCurveFromValues(
            (0, 100m), (1, 110m), (2, 90m), (3, 95m), (10, 115m));

        var (_, duration) = PerformanceCalculator.ComputeMaxDrawdown(curve);

        // Drawdown starts at day 1 (peak), recovers at day 10 → 9 days
        Assert.Equal(9, duration);
    }

    [Fact]
    public void Max_drawdown_zero_for_monotonic_increase()
    {
        var curve = MakeEquityCurveFromValues(
            (0, 100m), (1, 105m), (2, 110m), (3, 115m));

        var (maxDd, duration) = PerformanceCalculator.ComputeMaxDrawdown(curve);

        Assert.Equal(0m, maxDd);
        Assert.Equal(0, duration);
    }

    [Fact]
    public void Max_drawdown_duration_includes_ongoing_drawdown()
    {
        // Ends still in drawdown
        var curve = MakeEquityCurveFromValues(
            (0, 100m), (1, 110m), (2, 90m), (20, 95m));

        var (_, duration) = PerformanceCalculator.ComputeMaxDrawdown(curve);

        // Peak at day 1, still in drawdown at day 20 → 19 days
        Assert.Equal(19, duration);
    }

    // ── Trade Statistics ──────────────────────────────────────

    [Fact]
    public void Trade_statistics_computed()
    {
        var trades = new List<TradeRecord>
        {
            new() { PnL = 500m, HoldingDays = 5 },
            new() { PnL = 300m, HoldingDays = 3 },
            new() { PnL = -200m, HoldingDays = 2 },
            new() { PnL = -100m, HoldingDays = 1 },
        };

        var curve = MakeEquityCurve(100_000m, 100_500m, 30);
        var result = MakeResult(curve, trades);
        var metrics = PerformanceCalculator.Calculate(result);

        Assert.Equal(4, metrics.TotalTrades);
        Assert.Equal(2, metrics.WinningTrades);
        Assert.Equal(2, metrics.LosingTrades);
        Assert.Equal(50m, metrics.WinRate);
        Assert.Equal(400m, metrics.AverageWin);   // (500+300)/2
        Assert.Equal(-150m, metrics.AverageLoss);  // (-200+-100)/2
        Assert.Equal(500m, metrics.LargestWin);
        Assert.Equal(-200m, metrics.LargestLoss);
        Assert.Equal(2.75m, metrics.AverageHoldingDays); // (5+3+2+1)/4
    }

    [Fact]
    public void Profit_factor_computed()
    {
        var trades = new List<TradeRecord>
        {
            new() { PnL = 600m },
            new() { PnL = -200m },
        };

        var curve = MakeEquityCurve(100_000m, 100_400m, 30);
        var result = MakeResult(curve, trades);
        var metrics = PerformanceCalculator.Calculate(result);

        Assert.Equal(3m, metrics.ProfitFactor); // 600 / 200
    }

    [Fact]
    public void Expectancy_computed()
    {
        var trades = new List<TradeRecord>
        {
            new() { PnL = 1000m },
            new() { PnL = -500m },
            new() { PnL = 200m },
        };

        var curve = MakeEquityCurve(100_000m, 100_700m, 30);
        var result = MakeResult(curve, trades);
        var metrics = PerformanceCalculator.Calculate(result);

        // Expectancy = avg PnL = (1000 - 500 + 200) / 3 ≈ 233.33
        Assert.InRange(metrics.Expectancy, 233m, 234m);
    }

    // ── Calmar Ratio ──────────────────────────────────────────

    [Fact]
    public void Calmar_ratio_computed()
    {
        // CAGR = 20%, max DD = -10% → Calmar = 2.0
        var curve = MakeEquityCurveFromValues(
            (0, 100_000m), (30, 110_000m), (60, 99_000m), (365, 120_000m));

        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result);

        // Max DD = (99000 - 110000) / 110000 = -10%
        Assert.InRange(metrics.MaxDrawdownPercent, -10.1m, -9.9m);
        // Calmar = |CAGR / MaxDD| — just verify it's positive and reasonable
        Assert.True(metrics.CalmarRatio > 0, "Calmar should be positive for profitable strategy");
    }

    // ── Benchmark / Alpha / Beta ──────────────────────────────

    [Fact]
    public void Benchmark_return_computed()
    {
        var curve = MakeEquityCurve(100_000m, 110_000m, 252);
        var benchmark = MakeEquityCurve(100_000m, 108_000m, 252);

        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result, benchmarkEquityCurve: benchmark);

        Assert.InRange(metrics.BenchmarkReturn, 7.9m, 8.1m); // 8% return
        Assert.True(metrics.BenchmarkCagr > 0);
    }

    [Fact]
    public void Alpha_positive_when_outperforming()
    {
        var curve = MakeEquityCurve(100_000m, 130_000m, 252);
        var benchmark = MakeEquityCurve(100_000m, 110_000m, 252);

        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result, benchmarkEquityCurve: benchmark);

        Assert.True(metrics.Alpha > 0, $"Alpha should be positive when outperforming, got {metrics.Alpha}");
    }

    [Fact]
    public void Beta_one_for_identical_curves()
    {
        var curve = MakeEquityCurve(100_000m, 120_000m, 252);

        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result, benchmarkEquityCurve: curve);

        // When strategy == benchmark, beta should be ~1.0
        Assert.InRange(metrics.Beta, 0.99m, 1.01m);
    }

    [Fact]
    public void No_benchmark_gives_zero_alpha_beta()
    {
        var curve = MakeEquityCurve(100_000m, 120_000m, 252);
        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result, benchmarkEquityCurve: null);

        Assert.Equal(0m, metrics.Alpha);
        Assert.Equal(0m, metrics.Beta);
        Assert.Equal(0m, metrics.BenchmarkReturn);
    }

    // ── Monthly Returns ───────────────────────────────────────

    [Fact]
    public void Monthly_returns_matrix_generated()
    {
        var curve = new List<EquityPoint>
        {
            new(new DateTime(2024, 1, 2), 100_000m),
            new(new DateTime(2024, 1, 31), 102_000m),
            new(new DateTime(2024, 2, 28), 105_000m),
            new(new DateTime(2024, 3, 29), 103_000m),
        };

        var monthly = PerformanceCalculator.ComputeMonthlyReturns(curve);

        Assert.Equal(3, monthly.Count);
        Assert.True(monthly.ContainsKey("2024-01"));
        Assert.True(monthly.ContainsKey("2024-02"));
        Assert.True(monthly.ContainsKey("2024-03"));

        // Jan: 100k → 102k = +2%
        Assert.InRange(monthly["2024-01"], 1.9m, 2.1m);
        // Feb: 102k → 105k ≈ +2.94%
        Assert.InRange(monthly["2024-02"], 2.9m, 3.0m);
        // Mar: 105k → 103k ≈ -1.9%
        Assert.InRange(monthly["2024-03"], -2.0m, -1.8m);
    }

    // ── Yearly Returns ────────────────────────────────────────

    [Fact]
    public void Yearly_returns_breakdown()
    {
        var curve = new List<EquityPoint>
        {
            new(new DateTime(2023, 1, 2), 100_000m),
            new(new DateTime(2023, 12, 29), 115_000m),
            new(new DateTime(2024, 6, 28), 120_000m),
            new(new DateTime(2024, 12, 31), 125_000m),
        };

        var yearly = PerformanceCalculator.ComputeYearlyReturns(curve);

        Assert.Equal(2, yearly.Count);
        // 2023: 100k → 115k = +15%
        Assert.InRange(yearly[2023], 14.9m, 15.1m);
        // 2024: 115k → 125k ≈ +8.7%
        Assert.InRange(yearly[2024], 8.6m, 8.8m);
    }

    // ── Red Flags ─────────────────────────────────────────────

    [Fact]
    public void Red_flag_for_low_sharpe()
    {
        // Flat equity → Sharpe ≈ 0
        var curve = MakeEquityCurveFromValues(
            (0, 100_000m), (1, 100_001m), (2, 99_999m), (3, 100_000m));

        var result = MakeResult(curve, new List<TradeRecord>
        {
            new() { PnL = 1m },
            new() { PnL = -1m }
        });
        var metrics = PerformanceCalculator.Calculate(result);

        Assert.Contains(metrics.RedFlags, f => f.Contains("Sharpe"));
    }

    [Fact]
    public void Red_flag_when_underperforming_benchmark()
    {
        var curve = MakeEquityCurve(100_000m, 105_000m, 252); // +5%
        var benchmark = MakeEquityCurve(100_000m, 115_000m, 252); // +15%

        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result, benchmarkEquityCurve: benchmark);

        Assert.Contains(metrics.RedFlags, f => f.Contains("underperforms SPY"));
    }

    [Fact]
    public void Red_flag_for_low_profit_factor()
    {
        var trades = new List<TradeRecord>
        {
            new() { PnL = 100m },
            new() { PnL = -200m },
        };
        var curve = MakeEquityCurve(100_000m, 99_900m, 30);
        var result = MakeResult(curve, trades);
        var metrics = PerformanceCalculator.Calculate(result);

        Assert.Contains(metrics.RedFlags, f => f.Contains("Profit factor below 1.0"));
    }

    // ── Edge Cases ────────────────────────────────────────────

    [Fact]
    public void Empty_result_returns_zeroes()
    {
        var result = new BacktestEngineResult
        {
            Symbol = "TEST",
            InitialCapital = 100_000m,
            FinalEquity = 100_000m,
            Trades = new(),
            EquityCurve = new(),
        };

        var metrics = PerformanceCalculator.Calculate(result);

        Assert.Equal(0m, metrics.TotalReturn);
        Assert.Equal(0m, metrics.Cagr);
        Assert.Equal(0, metrics.TotalTrades);
        Assert.Equal(0m, metrics.SharpeRatio);
    }

    [Fact]
    public void No_trades_still_computes_equity_metrics()
    {
        // Equity can change without trades if benchmark comparison needed
        var curve = MakeEquityCurve(100_000m, 110_000m, 252);
        var result = MakeResult(curve);
        var metrics = PerformanceCalculator.Calculate(result);

        Assert.InRange(metrics.TotalReturn, 9.9m, 10.1m);
        Assert.True(metrics.Cagr > 0);
        Assert.Equal(0, metrics.TotalTrades);
    }

    [Fact]
    public void Risk_free_rate_configurable()
    {
        var curve = MakeEquityCurve(100_000m, 120_000m, 252);
        var returns = PerformanceCalculator.ComputeDailyReturns(curve);

        var sharpeHighRf = PerformanceCalculator.ComputeSharpe(returns, 10m);
        var sharpeLowRf = PerformanceCalculator.ComputeSharpe(returns, 1m);

        // Higher risk-free rate → lower Sharpe
        Assert.True(sharpeLowRf > sharpeHighRf,
            $"Lower RF ({sharpeLowRf}) should give higher Sharpe than higher RF ({sharpeHighRf})");
    }
}
