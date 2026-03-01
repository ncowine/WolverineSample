using TradingAssistant.Application.Backtesting;
using TradingAssistant.Application.Indicators;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Domain.Enums;

namespace TradingAssistant.Tests.Backtesting;

public class BacktestEngineTests
{
    private static readonly BacktestConfig TestConfig = new()
    {
        InitialCapital = 100_000m,
        SlippagePercent = 0m, // zero slippage for deterministic tests
        CommissionPerTrade = 0m
    };

    /// <summary>
    /// Build a simple strategy: entry when RSI &lt; 30, no exit conditions.
    /// </summary>
    private static StrategyDefinition SimpleRsiStrategy(decimal rsiThreshold = 30m) => new()
    {
        EntryConditions = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "LessThan", Value = rsiThreshold }
                }
            }
        },
        ExitConditions = new List<ConditionGroup>(),
        StopLoss = new StopLossConfig { Type = "FixedPercent", Multiplier = 5m },
        TakeProfit = new TakeProfitConfig { Type = "FixedPercent", Multiplier = 10m },
        PositionSizing = new PositionSizingConfig
        {
            RiskPercent = 1m,
            MaxPositions = 3,
            MaxPortfolioHeat = 10m
        },
        Filters = new TradeFilterConfig()
    };

    private static CandleWithIndicators MakeBar(
        DateTime date,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal rsi = 50m,
        decimal atr = 2m,
        long volume = 1_000_000)
    {
        return new CandleWithIndicators
        {
            Timestamp = date,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Interval = CandleInterval.Daily,
            Indicators = new IndicatorValues
            {
                Rsi = rsi,
                Atr = atr,
                SmaShort = close,
                SmaMedium = close,
                SmaLong = close,
                EmaShort = close,
                EmaMedium = close,
                EmaLong = close,
                BollingerUpper = close + 10,
                BollingerMiddle = close,
                BollingerLower = close - 10,
                IsWarmedUp = true
            }
        };
    }

    [Fact]
    public void Empty_bars_returns_empty_result()
    {
        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(Array.Empty<CandleWithIndicators>(), "TEST");

        Assert.Equal(100_000m, result.FinalEquity);
        Assert.Empty(result.Trades);
        Assert.Empty(result.EquityCurve);
    }

    [Fact]
    public void Single_bar_returns_empty_result()
    {
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100)
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Equal(100_000m, result.FinalEquity);
        Assert.Empty(result.Trades);
    }

    [Fact]
    public void No_signal_no_trades()
    {
        // RSI = 50 throughout, threshold is 30 → no entries
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 101, rsi: 50),
            MakeBar(new DateTime(2025, 1, 3), 101, 104, 100, 102, rsi: 50),
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Equal(100_000m, result.FinalEquity);
        Assert.Empty(result.Trades);
        Assert.Equal(2, result.EquityCurve.Count); // bars 1 and 2 (skip bar 0)
    }

    [Fact]
    public void Entry_signal_creates_pending_order_fills_next_bar()
    {
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50), // bar 0: no signal
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 101, rsi: 25), // bar 1: signal (RSI < 30)
            MakeBar(new DateTime(2025, 1, 3), 102, 104, 100, 103, rsi: 45), // bar 2: order fills at open=102
            MakeBar(new DateTime(2025, 1, 4), 103, 105, 101, 104, rsi: 50), // bar 3: position held
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        // Should have 1 trade (closed at end of backtest)
        Assert.Single(result.Trades);
        Assert.Equal("EndOfBacktest", result.Trades[0].ExitReason);
        Assert.Equal(102m, result.Trades[0].EntryPrice); // filled at bar 2 open
    }

    [Fact]
    public void Stop_loss_triggers_intrabar()
    {
        // 5% fixed stop from entry at 100 → stop at 95
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills at open=100, stop=95
            MakeBar(new DateTime(2025, 1, 4), 101, 101, 93, 94, rsi: 20), // low=93 ≤ stop=95 → stop hit
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal("StopLoss", result.Trades[0].ExitReason);
        Assert.Equal(95m, result.Trades[0].ExitPrice);
    }

    [Fact]
    public void Take_profit_triggers_intrabar()
    {
        // 10% fixed TP from entry at 100 → TP at 110
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills at open=100, TP=110
            MakeBar(new DateTime(2025, 1, 4), 105, 112, 104, 111, rsi: 70), // high=112 ≥ TP=110
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal("TakeProfit", result.Trades[0].ExitReason);
        Assert.Equal(110m, result.Trades[0].ExitPrice);
    }

    [Fact]
    public void Equity_curve_recorded_every_bar()
    {
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 101, rsi: 50),
            MakeBar(new DateTime(2025, 1, 3), 101, 104, 100, 102, rsi: 50),
            MakeBar(new DateTime(2025, 1, 4), 102, 105, 101, 103, rsi: 50),
            MakeBar(new DateTime(2025, 1, 5), 103, 106, 102, 104, rsi: 50),
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        // Bars 1 through 4 (bar 0 skipped as starting point)
        Assert.Equal(4, result.EquityCurve.Count);
        Assert.All(result.EquityCurve, ep => Assert.Equal(100_000m, ep.Value));
    }

    [Fact]
    public void Cash_decreases_on_buy_increases_on_sell()
    {
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills at 100
            MakeBar(new DateTime(2025, 1, 4), 101, 113, 100, 112, rsi: 70), // TP hit at 110
            MakeBar(new DateTime(2025, 1, 5), 112, 115, 111, 114, rsi: 75),
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(100m, trade.EntryPrice);
        Assert.Equal(110m, trade.ExitPrice);
        Assert.True(trade.PnL > 0, "Should be profitable");
    }

    [Fact]
    public void Max_positions_limit_respected()
    {
        var strategy = SimpleRsiStrategy();
        strategy.PositionSizing.MaxPositions = 1;

        // Two consecutive signals but max positions = 1 (and same symbol → no averaging down)
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal 1
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 25), // fills signal 1; signal 2 (blocked)
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 25), // blocked again
            MakeBar(new DateTime(2025, 1, 5), 103, 106, 102, 105, rsi: 50),
        };

        var engine = new BacktestEngine(strategy, TestConfig);
        var result = engine.Run(bars, "TEST");

        // Only 1 trade (closed at end)
        Assert.Single(result.Trades);
        // Blocked by no-averaging-down (fires before max positions for same symbol)
        Assert.Contains(result.Log, l => l.Contains("SKIP ENTRY"));
    }

    [Fact]
    public void Slippage_applied_on_fill()
    {
        var config = new BacktestConfig
        {
            InitialCapital = 100_000m,
            SlippagePercent = 1m, // 1% slippage
            CommissionPerTrade = 0m
        };

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills at 100 * 1.01 = 101
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 50),
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), config);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal(101m, result.Trades[0].EntryPrice); // 100 * 1.01
    }

    [Fact]
    public void Commission_deducted_from_pnl()
    {
        var config = new BacktestConfig
        {
            InitialCapital = 100_000m,
            SlippagePercent = 0m,
            CommissionPerTrade = 10m // $10 per trade
        };

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills
            MakeBar(new DateTime(2025, 1, 4), 101, 113, 100, 112, rsi: 70), // TP hit
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), config);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal(20m, result.Trades[0].Commission); // $10 entry + $10 exit
    }

    [Fact]
    public void Exit_conditions_close_positions()
    {
        var strategy = SimpleRsiStrategy();
        strategy.ExitConditions = new List<ConditionGroup>
        {
            new()
            {
                Timeframe = "Daily",
                Conditions = new List<Condition>
                {
                    new() { Indicator = "RSI", Comparison = "GreaterThan", Value = 70 }
                }
            }
        };

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // entry signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills
            MakeBar(new DateTime(2025, 1, 4), 101, 104, 100, 103, rsi: 75), // RSI>70 → exit signal
        };

        var engine = new BacktestEngine(strategy, TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal("ExitSignal", result.Trades[0].ExitReason);
        Assert.Equal(103m, result.Trades[0].ExitPrice); // closed at bar close
    }

    [Fact]
    public void Trade_filters_block_entry()
    {
        var strategy = SimpleRsiStrategy();
        strategy.Filters = new TradeFilterConfig { MinVolume = 5_000_000 };

        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50, volume: 1_000_000),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25, volume: 1_000_000), // signal, but volume too low
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45, volume: 1_000_000),
        };

        var engine = new BacktestEngine(strategy, TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Empty(result.Trades);
        Assert.True(result.Log.Any(l => l.Contains("trade filters")));
    }

    [Fact]
    public void Result_metadata_correct()
    {
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 101),
            MakeBar(new DateTime(2025, 1, 3), 101, 104, 100, 102),
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "AAPL");

        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(new DateTime(2025, 1, 1), result.StartDate);
        Assert.Equal(new DateTime(2025, 1, 3), result.EndDate);
        Assert.Equal(100_000m, result.InitialCapital);
    }

    [Fact]
    public void Win_rate_calculated_correctly()
    {
        // Entry at 100, TP at 110 → winning trade
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25),
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45),
            MakeBar(new DateTime(2025, 1, 4), 105, 112, 104, 111, rsi: 70),
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal(1, result.WinningTrades);
        Assert.Equal(100m, result.WinRate);
    }

    [Fact]
    public void Holding_days_tracked()
    {
        var bars = new[]
        {
            MakeBar(new DateTime(2025, 1, 1), 100, 102, 98, 100, rsi: 50),
            MakeBar(new DateTime(2025, 1, 2), 100, 103, 99, 100, rsi: 25), // signal
            MakeBar(new DateTime(2025, 1, 3), 100, 102, 98, 101, rsi: 45), // fills
            MakeBar(new DateTime(2025, 1, 6), 101, 113, 100, 112, rsi: 70), // TP hit, 3 days later
        };

        var engine = new BacktestEngine(SimpleRsiStrategy(), TestConfig);
        var result = engine.Run(bars, "TEST");

        Assert.Single(result.Trades);
        Assert.Equal(3, result.Trades[0].HoldingDays); // Jan 3 → Jan 6
    }
}
