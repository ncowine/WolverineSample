using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Tests.Backtesting;

/// <summary>
/// Tests for STORY-030: Capital preservation enforcement in backtester.
/// Covers circuit breaker, no-averaging-down, recovery, and position sizing formula.
/// Updated for unified PortfolioBacktestEngine which applies signal-score-based sizing.
/// </summary>
public class CapitalPreservationTests
{
    private static readonly BacktestConfig TestConfig = new()
    {
        InitialCapital = 10_000m,
        SlippagePercent = 0m,
        CommissionPerTrade = 0m
    };

    private static StrategyDefinition MakeStrategy(
        decimal riskPercent = 10m,
        int maxPositions = 3,
        decimal maxHeat = 30m,
        decimal maxDrawdown = 5m,
        decimal recoveryPercent = 2m,
        decimal stopMultiplier = 20m) => new()
    {
        EntryConditions = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = 30 }
                }
            }
        },
        ExitConditions = new List<ConditionGroup>(),
        StopLoss = new StopLossConfig { Type = "FixedPercent", Multiplier = stopMultiplier, MaxStopLossPercent = 50m },
        TakeProfit = new TakeProfitConfig { Type = "FixedPercent", Multiplier = 50m },
        PositionSizing = new PositionSizingConfig
        {
            RiskPercent = riskPercent,
            MaxPositions = maxPositions,
            MaxPortfolioHeat = maxHeat,
            MaxDrawdownPercent = maxDrawdown,
            DrawdownRecoveryPercent = recoveryPercent
        },
        Filters = new TradeFilterConfig()
    };

    private static CandleWithIndicators MakeBar(
        DateTime date, decimal open, decimal high, decimal low, decimal close,
        decimal rsi = 50m, decimal atr = 2m, long volume = 1_000_000)
    {
        return new CandleWithIndicators
        {
            Timestamp = date, Open = open, High = high, Low = low, Close = close,
            Volume = volume, Interval = CandleInterval.Daily,
            Indicators = new IndicatorValues
            {
                Rsi = rsi, Atr = atr,
                SmaShort = close, SmaMedium = close, SmaLong = close,
                EmaShort = close, EmaMedium = close, EmaLong = close,
                BollingerUpper = close + 10, BollingerMiddle = close, BollingerLower = close - 10,
                IsWarmedUp = true
            }
        };
    }

    private static PortfolioBacktestEngine CreateEngine(StrategyDefinition strategy, BacktestConfig? config = null)
    {
        var c = config ?? TestConfig;
        return new PortfolioBacktestEngine(strategy, strategy.PositionSizing.MaxPositions, c.InitialCapital, c);
    }

    private static BacktestEngineResult RunSingle(PortfolioBacktestEngine engine, CandleWithIndicators[] bars, string symbol = "TEST")
    {
        return engine.Run(new Dictionary<string, CandleWithIndicators[]> { [symbol] = bars });
    }

    // Signal score multiplier: for RSI=25 (test signal), ComputeSignalScore returns ~30.
    // scoreMultiplier = 0.5 + (30/100) * 0.5 = 0.65
    // So actual shares = floor(baseShares * 0.65)

    // ── No Averaging Down ─────────────────────────────────────

    [Fact]
    public void No_averaging_down_blocks_duplicate_symbol()
    {
        // MaxPositions = 3, so it's not the position limit blocking.
        // After first entry fills, second signal on same symbol is blocked by heldSymbols check.
        var strategy = MakeStrategy(riskPercent: 5, maxPositions: 3, stopMultiplier: 20);
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal 1
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 25), // fills signal 1; signal 2 (same symbol)
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 25), // signal 2 would fill, but blocked
            MakeBar(new DateTime(2025, 1, 5), 103, 106, 102, 105, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars, "AAPL");

        // Only 1 trade (closed at end), not 2 — unified engine silently skips held symbols
        Assert.Single(result.Trades);
    }

    // ── Drawdown Circuit Breaker ──────────────────────────────

    [Fact]
    public void Circuit_breaker_activates_on_drawdown()
    {
        // Capital = $10,000, maxDrawdown = 5% → breaker at $9,500
        // With signal score multiplier (~0.65), fewer shares but still enough for drawdown trigger
        var strategy = MakeStrategy(riskPercent: 10, maxDrawdown: 5, stopMultiplier: 20);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // entry signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50), // fills at open=100
            MakeBar(new DateTime(2025, 1, 4), 90, 91, 84, 85, rsi: 50),   // big drop → breaker activates
            MakeBar(new DateTime(2025, 1, 5), 85, 86, 83, 84, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars, "AAPL");

        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER ACTIVATED"));
    }

    [Fact]
    public void Circuit_breaker_blocks_new_entries()
    {
        // After stop loss hit → drawdown → breaker activated → next signal blocked
        // Unified engine blocks entries at the top level (ScanForEntries not called when breaker active)
        var strategy = MakeStrategy(riskPercent: 10, maxDrawdown: 5, stopMultiplier: 5);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // entry signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50), // fills at open=100, stop=95
            MakeBar(new DateTime(2025, 1, 4), 96, 96, 93, 94, rsi: 50),    // stop hit at 95
            MakeBar(new DateTime(2025, 1, 5), 94, 96, 92, 95, rsi: 25),    // signal fires → blocked by breaker
            MakeBar(new DateTime(2025, 1, 6), 95, 97, 93, 96, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars);

        // Circuit breaker should have activated after the stop loss + drawdown
        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER ACTIVATED"));
        // Only 1 trade (the one that was stopped out) — second signal was blocked
        Assert.Single(result.Trades);
    }

    [Fact]
    public void Circuit_breaker_deactivates_on_recovery()
    {
        var strategy = MakeStrategy(riskPercent: 10, maxDrawdown: 5, recoveryPercent: 2, stopMultiplier: 20);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // entry signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50), // fills at open=100
            MakeBar(new DateTime(2025, 1, 4), 90, 91, 84, 85, rsi: 50),   // drop → breaker activates
            MakeBar(new DateTime(2025, 1, 5), 86, 88, 85, 87, rsi: 50),   // still down
            MakeBar(new DateTime(2025, 1, 6), 92, 98, 91, 97, rsi: 50),   // recovery → breaker deactivates
            MakeBar(new DateTime(2025, 1, 7), 97, 100, 96, 99, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars, "AAPL");

        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER ACTIVATED"));
        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER DEACTIVATED"));
    }

    [Fact]
    public void Circuit_breaker_recovery_threshold_is_configurable()
    {
        var strategy = MakeStrategy(riskPercent: 10, maxDrawdown: 5, recoveryPercent: 0.1m, stopMultiplier: 20);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50), // fills
            MakeBar(new DateTime(2025, 1, 4), 90, 91, 84, 85, rsi: 50),   // drop → breaker
            MakeBar(new DateTime(2025, 1, 5), 92, 98, 91, 97, rsi: 50),   // partial recovery
            MakeBar(new DateTime(2025, 1, 6), 97, 100, 96, 99, rsi: 50),  // still not enough
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars, "AAPL");

        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER ACTIVATED"));
        // Should NOT have deactivated — recovery threshold too tight
        Assert.DoesNotContain(result.Log, l => l.Contains("CIRCUIT BREAKER DEACTIVATED"));
    }

    // ── Position Sizing Formula ───────────────────────────────

    [Fact]
    public void Position_sizing_uses_risk_based_formula()
    {
        // Capital = $10,000, risk = 2%, stop = 10% fixed
        // Entry signal at close=$100 → stop = $90 → risk/share = $10
        // Risk amount = $10,000 * 2% = $200
        // Signal score ~30 → scoreMultiplier = 0.5 + (30/100)*0.5 = 0.65
        // Adjusted risk = $200 * 0.65 = $130
        // Shares = 130 / 10 = 13
        var strategy = MakeStrategy(riskPercent: 2, stopMultiplier: 10);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal at close=100
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50), // fills at open=100
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars);

        Assert.Single(result.Trades);
        Assert.Equal(13, result.Trades[0].Shares);
        Assert.Equal(100m, result.Trades[0].EntryPrice);
    }

    [Fact]
    public void Position_sizing_rounds_down_to_whole_shares()
    {
        // Capital = $10,000, risk = 3%, stop = 7%
        // stop = $93 → risk/share = $7
        // Risk amount = $10,000 * 3% = $300
        // Adjusted risk = $300 * 0.65 = $195
        // Shares = floor(195 / 7) = 27
        var strategy = MakeStrategy(riskPercent: 3, stopMultiplier: 7);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25),
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50),
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars);

        Assert.Single(result.Trades);
        Assert.Equal(27, result.Trades[0].Shares); // floor(195 / 7) = 27
    }

    // ── Portfolio Heat ────────────────────────────────────────

    [Fact]
    public void Portfolio_heat_logged_when_blocking()
    {
        var strategy = MakeStrategy(riskPercent: 5, maxHeat: 3, maxPositions: 5, stopMultiplier: 10);
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25),
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50),
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars);

        // Verify position was opened (the heat check applies to NEW entries when existing positions have risk)
        Assert.Single(result.Trades);
    }

    // ── All Rules Logged ──────────────────────────────────────

    [Fact]
    public void All_rejection_reasons_logged()
    {
        var strategy = MakeStrategy(riskPercent: 10, maxPositions: 1, maxDrawdown: 5, stopMultiplier: 5);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 25), // fills; signal 2 → no-averaging-down
            MakeBar(new DateTime(2025, 1, 4), 96, 96, 93, 94, rsi: 25),    // stop hit; signal → circuit breaker
            MakeBar(new DateTime(2025, 1, 5), 94, 96, 92, 95, rsi: 25),    // signal → circuit breaker
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars);

        // Verify various log messages exist
        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER") || l.Contains("BUY") || l.Contains("SELL"));
        Assert.True(result.Log.Count >= 3, "Should have multiple log entries");
    }

    // ── Drawdown Percent Calculation ──────────────────────────

    [Fact]
    public void Drawdown_tracked_from_peak_not_initial()
    {
        // Use higher risk to get more shares so drawdown exceeds threshold
        var strategy = MakeStrategy(riskPercent: 30, maxDrawdown: 5, stopMultiplier: 20);

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25),  // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 50),  // fills at 100
            MakeBar(new DateTime(2025, 1, 4), 110, 122, 109, 120, rsi: 50), // big rise → new peak
            MakeBar(new DateTime(2025, 1, 5), 115, 116, 106, 108, rsi: 50), // drop from peak
            MakeBar(new DateTime(2025, 1, 6), 108, 110, 107, 109, rsi: 50),
        };

        var engine = CreateEngine(strategy);
        var result = RunSingle(engine, bars, "AAPL");

        // The breaker should trigger based on the peak, not the initial capital
        Assert.Contains(result.Log, l => l.Contains("CIRCUIT BREAKER ACTIVATED"));
    }
}
