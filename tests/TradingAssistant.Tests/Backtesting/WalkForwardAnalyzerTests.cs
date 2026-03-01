using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Tests.Backtesting;

public class WalkForwardAnalyzerTests
{
    // ── Helpers ───────────────────────────────────────────────

    private static CandleWithIndicators[] MakeBars(int count, DateTime? start = null)
    {
        var startDate = start ?? new DateTime(2020, 1, 2);
        var bars = new CandleWithIndicators[count];
        for (var i = 0; i < count; i++)
        {
            var price = 100m + i * 0.1m; // slowly rising
            bars[i] = new CandleWithIndicators
            {
                Timestamp = startDate.AddDays(i),
                Open = price - 0.5m, High = price + 1m, Low = price - 1m, Close = price,
                Volume = 1_000_000,
                Interval = CandleInterval.Daily,
                Indicators = new IndicatorValues
                {
                    Rsi = 50, Atr = 2, SmaShort = price, SmaMedium = price,
                    BollingerUpper = price + 10, BollingerMiddle = price, BollingerLower = price - 10,
                    IsWarmedUp = true
                }
            };
        }
        return bars;
    }

    /// <summary>
    /// Fake backtest runner: param "A" controls return. Higher A = better.
    /// Generates an equity curve with zigzag for valid Sharpe.
    /// </summary>
    private static BacktestEngineResult FakeRunner(ParameterSet paramSet, CandleWithIndicators[] bars)
    {
        var a = paramSet.TryGet("A", out var val) ? val : 10m;
        var initial = 100_000m;
        var returnPct = a / 100m; // A=10 → 10% return over period
        var final_ = initial * (1 + returnPct);
        var noise = (final_ - initial) * 0.003m;

        var curve = new List<EquityPoint>();
        for (var i = 0; i < bars.Length; i++)
        {
            var t = bars.Length > 1 ? (decimal)i / (bars.Length - 1) : 0m;
            var baseVal = initial + (final_ - initial) * t;
            var wiggle = (i % 2 == 0) ? noise : -noise;
            curve.Add(new EquityPoint(bars[i].Timestamp, baseVal + wiggle));
        }

        return new BacktestEngineResult
        {
            Symbol = "TEST",
            StartDate = bars[0].Timestamp,
            EndDate = bars[^1].Timestamp,
            InitialCapital = initial,
            FinalEquity = final_,
            EquityCurve = curve,
            Trades = new List<TradeRecord> { new() { PnL = final_ - initial, HoldingDays = bars.Length } },
            Log = new()
        };
    }

    private static ParameterSpace SimpleSpace() => new()
    {
        Parameters = new List<ParameterDefinition>
        {
            new() { Name = "A", Min = 5, Max = 25, Step = 5 } // 5 values: 5,10,15,20,25
        }
    };

    // ── Window Generation ─────────────────────────────────────

    [Fact]
    public void Rolling_windows_generated_correctly()
    {
        // 800 bars, IS=300, OOS=100 → window size=400
        // Window 1: IS[0..299], OOS[300..399]
        // Window 2: IS[100..399], OOS[400..499] (slide by OOS=100)
        // Window 3: IS[200..499], OOS[500..599]
        // Window 4: IS[300..599], OOS[600..699]
        // Window 5: IS[400..699], OOS[700..799]
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300,
            OutOfSampleDays = 100,
            Mode = WalkForwardMode.Rolling
        };

        var windows = WalkForwardAnalyzer.GenerateWindows(bars, config);

        Assert.Equal(5, windows.Count);
        Assert.Equal((0, 299, 300, 399), windows[0]);
        Assert.Equal((100, 399, 400, 499), windows[1]);
    }

    [Fact]
    public void Anchored_windows_generated_correctly()
    {
        // 800 bars, IS=300, OOS=100
        // Window 1: IS[0..299], OOS[300..399]
        // Window 2: IS[0..399], OOS[400..499] (IS grows)
        // Window 3: IS[0..499], OOS[500..599]
        // Window 4: IS[0..599], OOS[600..699]
        // Window 5: IS[0..699], OOS[700..799]
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300,
            OutOfSampleDays = 100,
            Mode = WalkForwardMode.Anchored
        };

        var windows = WalkForwardAnalyzer.GenerateWindows(bars, config);

        Assert.Equal(5, windows.Count);
        // All start at 0
        Assert.All(windows, w => Assert.Equal(0, w.IsStart));
        // IS end grows
        Assert.Equal(299, windows[0].IsEnd);
        Assert.Equal(399, windows[1].IsEnd);
    }

    [Fact]
    public void Insufficient_data_returns_no_windows()
    {
        var bars = MakeBars(100); // less than IS + OOS
        var config = new WalkForwardConfig { InSampleDays = 300, OutOfSampleDays = 100 };

        var windows = WalkForwardAnalyzer.GenerateWindows(bars, config);
        Assert.Empty(windows);
    }

    // ── Full Walk-Forward Analysis ────────────────────────────

    [Fact]
    public void Analyze_produces_windows_with_metrics()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300,
            OutOfSampleDays = 100,
            Mode = WalkForwardMode.Rolling,
            RiskFreeRate = 4.5m,
            OptimizationTopN = 3
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.True(result.Windows.Count >= 2, $"Expected multiple windows, got {result.Windows.Count}");
        Assert.All(result.Windows, w =>
        {
            Assert.True(w.InSampleSharpe != 0 || w.OutOfSampleSharpe != 0);
            Assert.True(w.WindowNumber > 0);
        });
    }

    [Fact]
    public void Best_params_have_highest_A()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        // Each window should pick A=25 as best (highest return → highest Sharpe)
        Assert.All(result.Windows, w => Assert.Equal(25m, w.BestParameters["A"]));
    }

    [Fact]
    public void Overfitting_score_computed_per_window()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.All(result.Windows, w =>
        {
            // With same fake runner, IS and OOS should produce similar Sharpes
            // so overfitting score should be relatively low
            Assert.True(w.OverfittingScore < 1.0m,
                $"Window {w.WindowNumber}: overfitting score {w.OverfittingScore} seems too high");
        });
    }

    [Fact]
    public void Efficiency_computed_per_window()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.All(result.Windows, w =>
        {
            // Efficiency = OOS/IS. With similar data, should be non-zero
            Assert.True(w.Efficiency != 0,
                $"Window {w.WindowNumber}: efficiency should not be zero");
        });
    }

    [Fact]
    public void Aggregate_metrics_computed()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.True(result.AverageInSampleSharpe != 0);
        Assert.True(result.AverageOutOfSampleSharpe != 0);
        Assert.True(result.Windows.Count > 0);
    }

    [Fact]
    public void Overfitting_grade_assigned()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        // Grade should be one of the valid values
        Assert.True(Enum.IsDefined(typeof(OverfittingGrade), result.Grade));
    }

    [Fact]
    public void Blessed_parameters_from_best_oos_window()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        // Blessed params should exist and have the "A" parameter
        Assert.True(result.BlessedParameters.TryGet("A", out var a));
        Assert.Equal(25m, a); // best param in all windows
    }

    [Fact]
    public void Aggregated_equity_curve_built()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.True(result.AggregatedEquityCurve.Count > 0,
            "Aggregated equity curve should have points from OOS windows");
    }

    [Fact]
    public void Insufficient_data_returns_warning()
    {
        var bars = MakeBars(50); // too short
        var config = new WalkForwardConfig { InSampleDays = 300, OutOfSampleDays = 100 };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.Empty(result.Windows);
        Assert.Contains(result.Warnings, w => w.Contains("Insufficient data"));
    }

    [Fact]
    public void Anchored_mode_works()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Anchored
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.True(result.Windows.Count >= 2);
        // First window IS should be shorter than last window IS (anchored grows)
        var firstIsRange = result.Windows[0].InSampleEnd - result.Windows[0].InSampleStart;
        var lastIsRange = result.Windows[^1].InSampleEnd - result.Windows[^1].InSampleStart;
        Assert.True(lastIsRange > firstIsRange,
            "Anchored mode: last window IS range should be larger than first");
    }

    [Fact]
    public void Elapsed_time_tracked()
    {
        var bars = MakeBars(800);
        var config = new WalkForwardConfig
        {
            InSampleDays = 300, OutOfSampleDays = 100, Mode = WalkForwardMode.Rolling
        };

        var result = WalkForwardAnalyzer.Analyze(bars, SimpleSpace(), FakeRunner, config);

        Assert.True(result.ElapsedTime >= TimeSpan.Zero);
    }
}
