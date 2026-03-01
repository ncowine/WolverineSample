# Sprint Plan: Quantitative Trading Engine

**Date:** 2026-03-01
**Project Level:** 3
**Total Stories:** 27
**Total Points:** 154
**Phases:** 6 (Data → Indicators → Engine → Optimization → Screening → Frontend)

---

## Executive Summary

Build a production-grade quantitative backtesting and stock screening system on top of the existing TradingAssistant backend. The system ingests real market data (Yahoo Finance), computes technical indicators across multiple timeframes, backtests strategies with realistic simulation, optimizes parameters via walk-forward analysis, and surfaces trade signals ranked by a multi-factor confidence score. **Capital preservation is the #1 design goal** — every component enforces risk limits. The benchmark is S&P 500 buy-and-hold: if a strategy can't beat it risk-adjusted, it's flagged and excluded from screening.

### What Exists Today (Gaps)

| Component | Current State | Gap |
|-----------|--------------|-----|
| Market data | 8 stocks, 90 days, seed-only | Need 50+ stocks, 5+ years, real Yahoo Finance data |
| Indicators | `IndicatorType` enum only | No calculation engine — SMA/EMA/RSI/MACD/BB are just labels |
| Backtest engine | Price-change threshold comparison | Not a real engine — no indicator evaluation, no position sizing, no slippage |
| Metrics | WinRate, TotalReturn, MaxDD, SharpeRatio | Sharpe formula is wrong (return/drawdown). Missing CAGR, Sortino, Alpha/Beta, Calmar |
| Optimization | None | Need walk-forward with in-sample/out-of-sample validation |
| Risk management | None | Need per-trade risk limits, portfolio heat, drawdown circuit breaker |
| Screening | None | Need universe scanning with confidence scoring |
| Frontend | Route stubs only | Need backtest UI, screener, charts with indicators |

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Angular Frontend                         │
│  Backtest Config → Results Dashboard → Screener → Trade Entry   │
└──────────────────────────┬──────────────────────────────────────┘
                           │ REST API
┌──────────────────────────┴──────────────────────────────────────┐
│                     TradingAssistant.Api                         │
│  /api/market-data/ingest  /api/backtests  /api/screener         │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────┐
│                  TradingAssistant.Application                    │
│                                                                  │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │  Indicator   │  │  Backtest    │  │  Screener              │  │
│  │  Engine      │  │  Engine      │  │  Engine                │  │
│  │             │  │             │  │                        │  │
│  │  SMA/EMA    │  │  Event-loop  │  │  Universe scan         │  │
│  │  RSI/MACD   │  │  Order sim   │  │  Indicator eval        │  │
│  │  BB/ATR     │  │  Position    │  │  Confidence scoring    │  │
│  │  Stochastic │  │  tracking    │  │  Signal ranking        │  │
│  │  OBV/VWAP   │  │  Slippage    │  │  Grade A-F             │  │
│  │  S/R levels │  │  Commission  │  │                        │  │
│  └──────┬──────┘  │  Risk mgmt   │  └────────────────────────┘  │
│         │         └──────┬───────┘                               │
│         │                │                                       │
│  ┌──────┴────────────────┴───────────────────────────────────┐  │
│  │              Optimizer (Walk-Forward)                       │  │
│  │  Parameter grid → In-sample backtest → Out-of-sample test  │  │
│  │  Overfitting detection → Best params → Feed to screener    │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────┴──────────────────────────────────────┐
│                  Market Data Pipeline                            │
│  Yahoo Finance API → Daily/Weekly/Monthly candles → SQLite      │
│  S&P 500 (SPY) benchmark → Stock universe management            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Capital Preservation Rules (Enforced Everywhere)

These are not optional. Every backtest run and every screener signal respects them:

| Rule | Default | Configurable |
|------|---------|-------------|
| Max risk per trade | 1% of account | 0.5% – 2% |
| Max open positions | 6 | 3 – 10 |
| Max portfolio heat (total risk) | 6% | 3% – 10% |
| Max drawdown circuit breaker | 15% | 10% – 25% |
| Mandatory stop loss | ATR-based (2x ATR) | Fixed %, ATR-based, support-based |
| Min risk/reward ratio | 2:1 | 1.5:1 – 5:1 |
| No averaging down | Always enforced | Not configurable |
| Position sizing | % risk / (entry - stop) | Kelly optional |

---

## Confidence Scoring System

Each signal is graded A through F based on weighted factors:

| Factor | Weight | Scoring |
|--------|--------|---------|
| Timeframe alignment | 25% | Higher TF trend matches lower TF entry direction |
| Number of confirmations | 25% | How many indicators agree (3+ = strong) |
| Volume confirmation | 15% | Above-average volume on signal bar |
| Risk/Reward ratio | 15% | Higher R:R = higher score |
| Historical win rate | 10% | Backtest accuracy for this setup type |
| Volatility regime | 10% | ATR within normal range (not too volatile, not too quiet) |

**Grades:**
- **A (90-100):** All timeframes aligned, 4+ confirmations, volume confirmed, R:R > 3:1
- **B (75-89):** Strong alignment, 3+ confirmations, decent volume
- **C (60-74):** Partial alignment, 2 confirmations, acceptable R:R
- **D (40-59):** Weak setup, single confirmation — do not trade
- **F (<40):** Conflicting signals — avoid

**Only A and B grade signals are surfaced to the user for trading.**

---

## Performance Metrics (vs S&P 500)

Every backtest computes these. Red flag if strategy underperforms SPY buy-and-hold:

| Metric | Formula | Why It Matters |
|--------|---------|----------------|
| CAGR | (EndValue/StartValue)^(1/Years) - 1 | Annualized return |
| Sharpe Ratio | (AvgReturn - RiskFreeRate) / StdDev(Returns) | Risk-adjusted return |
| Sortino Ratio | (AvgReturn - RiskFreeRate) / DownsideStdDev | Penalizes only downside |
| Max Drawdown | Largest peak-to-trough decline | Capital preservation |
| Max DD Duration | Longest time to recover from drawdown | Recovery speed |
| Calmar Ratio | CAGR / MaxDrawdown | Return per unit of drawdown |
| Win Rate | Winning trades / Total trades | Consistency |
| Profit Factor | Gross profit / Gross loss | Edge size |
| Expectancy | (WinRate × AvgWin) - (LossRate × AvgLoss) | Expected $ per trade |
| Alpha | Strategy return - Beta × Market return | Excess return vs market |
| Beta | Covariance(Strategy, Market) / Variance(Market) | Market correlation |
| **SPY Comparison** | Strategy CAGR vs SPY CAGR (same period) | **Must beat this** |

---

## Story Inventory (Dependency Order)

### Phase 1: Data Foundation

---

#### STORY-020: Yahoo Finance data provider integration

**Priority:** Must Have

**User Story:**
As a trader, I want real historical market data so that backtests produce meaningful results instead of random seed data.

**Acceptance Criteria:**
- [ ] HTTP client fetches daily OHLCV data from Yahoo Finance (chart API)
- [ ] Rate limiting (max 5 requests/second, configurable)
- [ ] Retry with exponential backoff on 429/5xx
- [ ] Adjusted close prices used (accounts for splits/dividends)
- [ ] Can fetch 1 day to 20 years of history per symbol
- [ ] Returns strongly-typed `YahooCandle` records

**Technical Notes:**
- Yahoo Finance chart API: `https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=5y&interval=1d`
- No API key needed. Parse JSON response for timestamps, open, high, low, close, adjclose, volume
- Create `IMarketDataProvider` interface in Application layer, `YahooFinanceProvider` in Infrastructure
- Handle stock splits via adjusted close

**Points:** 5

---

#### STORY-021: Historical data ingestion and multi-timeframe storage

**Priority:** Must Have

**User Story:**
As a trader, I want daily, weekly, and monthly candle data stored locally so the indicator engine can compute across multiple timeframes.

**Acceptance Criteria:**
- [ ] Ingestion command fetches data for a symbol and stores in MarketDataDb
- [ ] Deduplication: skip candles that already exist (upsert by symbol+date+interval)
- [ ] Daily → Weekly aggregation (Mon-Fri grouped, OHLCV rules)
- [ ] Daily → Monthly aggregation (calendar month grouped)
- [ ] `CandleInterval.Monthly` added to enum
- [ ] Composite index on (StockId, Interval, Timestamp) for query performance
- [ ] Bulk insert for initial backfill (batch SaveChanges)

**Technical Notes:**
- Weekly: Open = Monday's open, Close = Friday's close, High = max of week, Low = min of week, Volume = sum
- Monthly: Same logic across calendar month
- Add `CandleInterval.Monthly` to existing enum
- New command: `IngestMarketDataCommand(string Symbol, int YearsBack)`

**Points:** 5

---

#### STORY-022: Stock universe and S&P 500 benchmark management

**Priority:** Must Have

**User Story:**
As a trader, I want to define which stocks to screen and have S&P 500 (SPY) data as benchmark so every backtest is compared against buy-and-hold.

**Acceptance Criteria:**
- [ ] `StockUniverse` entity: name, list of symbols, isActive
- [ ] Pre-built universe: "S&P 500 Top 50" (50 most liquid S&P components)
- [ ] SPY (S&P 500 ETF) always ingested as benchmark
- [ ] API endpoints: create universe, list universes, add/remove symbols
- [ ] Benchmark data queryable by date range for any backtest period

**Technical Notes:**
- Hard-code top 50 S&P symbols initially (AAPL, MSFT, GOOGL, AMZN, NVDA, META, TSLA, BRK.B, JPM, V, UNH, XOM, JNJ, WMT, MA, PG, AVGO, HD, CVX, MRK, ABBV, PEP, KO, COST, ADBE, CRM, TMO, CSCO, ACN, MCD, ABT, NKE, DHR, NFLX, AMD, LIN, TXN, QCOM, PM, INTC, CMCSA, UNP, ORCL, LOW, UPS, MS, GS, BLK, CAT, BA)
- Store as a simple entity with JSON symbol list

**Points:** 3

---

#### STORY-023: Bulk data backfill background service

**Priority:** Must Have

**User Story:**
As a trader, I want to trigger a bulk data load that fetches 5+ years of history for all stocks in a universe so the system is ready for backtesting.

**Acceptance Criteria:**
- [ ] `BackfillCommand(Guid UniverseId, int YearsBack)` triggers bulk ingestion
- [ ] BackgroundService processes symbols sequentially (respects rate limits)
- [ ] Progress tracking: X/N symbols complete, stored in DB
- [ ] Weekly + Monthly candles auto-generated after daily ingestion
- [ ] Incremental mode: only fetch missing dates (daily update)
- [ ] API endpoint to check backfill status

**Technical Notes:**
- Process 1 symbol at a time with 500ms delay between Yahoo requests
- For 50 symbols × 5 years: ~50 requests, takes ~30 seconds
- Store backfill status in a `BackfillJob` entity (status, progress, errors)
- Reuse for daily incremental updates

**Points:** 5

---

### Phase 2: Technical Indicators Engine

---

#### STORY-024: Indicator calculation framework and trend indicators

**Priority:** Must Have

**User Story:**
As a backtester, I want accurate SMA, EMA, and WMA calculations so strategies can use moving average signals.

**Acceptance Criteria:**
- [ ] `IIndicatorCalculator` interface: `decimal[] Calculate(decimal[] prices, int period)`
- [ ] `SmaCalculator`: Simple moving average (sum of N / N)
- [ ] `EmaCalculator`: Exponential moving average (multiplier = 2/(period+1))
- [ ] `WmaCalculator`: Weighted moving average (recent prices weighted more)
- [ ] Moving average crossover detection: `CrossoverDetector.Detect(fast[], slow[])` → returns crossover points
- [ ] All calculators return `decimal[]` aligned to input array (NaN/0 for warmup period)
- [ ] Unit tests: verify against known calculated values (use investopedia examples)

**Technical Notes:**
- Pure static methods, no side effects, no DB dependency
- Place in `TradingAssistant.Application/Indicators/` namespace
- EMA formula: EMA_today = Price × k + EMA_yesterday × (1 - k), where k = 2/(period+1)
- Warmup: first N-1 values are 0/null, Nth value uses SMA as seed for EMA

**Points:** 5

---

#### STORY-025: Momentum indicators (RSI, MACD, Stochastic)

**Priority:** Must Have

**User Story:**
As a backtester, I want RSI, MACD, and Stochastic Oscillator calculations for momentum-based strategies.

**Acceptance Criteria:**
- [ ] `RsiCalculator`: RSI(period) using Wilder's smoothing method
- [ ] `MacdCalculator`: MACD line (EMA12 - EMA26), Signal line (EMA9 of MACD), Histogram
  - Returns `MacdResult { decimal[] Macd, decimal[] Signal, decimal[] Histogram }`
- [ ] `StochasticCalculator`: %K (fast), %D (slow) with configurable periods
  - Returns `StochasticResult { decimal[] K, decimal[] D }`
- [ ] Divergence detection: price makes new high but indicator doesn't (bearish), vice versa
- [ ] Unit tests with known values

**Technical Notes:**
- RSI: Use Wilder's smoothing (not simple average) — AvgGain = (prevAvgGain × 13 + currentGain) / 14
- MACD standard: fast=12, slow=26, signal=9 (but configurable)
- Stochastic: %K = (Close - LowestLow(N)) / (HighestHigh(N) - LowestLow(N)) × 100

**Points:** 5

---

#### STORY-026: Volatility indicators (ATR, Bollinger Bands) and volume indicators (OBV)

**Priority:** Must Have

**User Story:**
As a backtester, I want ATR for stop-loss calculation, Bollinger Bands for volatility breakouts, and OBV for volume confirmation.

**Acceptance Criteria:**
- [ ] `AtrCalculator`: Average True Range using Wilder's smoothing
  - True Range = max(High-Low, |High-PrevClose|, |Low-PrevClose|)
- [ ] `BollingerBandsCalculator`: Upper/Middle/Lower bands, Bandwidth, %B
  - Returns `BollingerResult { decimal[] Upper, decimal[] Middle, decimal[] Lower, decimal[] Bandwidth, decimal[] PercentB }`
- [ ] `ObvCalculator`: On-Balance Volume (cumulative volume based on price direction)
- [ ] `VolumeProfileCalculator`: Volume moving average for "above average volume" detection
- [ ] Unit tests

**Technical Notes:**
- ATR is critical for position sizing: stop = entry - 2×ATR, risk = shares × (entry - stop)
- Bollinger default: 20-period SMA, 2 standard deviations
- %B = (Price - LowerBand) / (UpperBand - LowerBand) — measures where price is within bands

**Points:** 5

---

#### STORY-027: Multi-timeframe indicator orchestrator

**Priority:** Must Have

**User Story:**
As a backtester, I want to compute indicators on daily, weekly, and monthly data simultaneously so strategies can use multi-timeframe confirmation.

**Acceptance Criteria:**
- [ ] `IndicatorOrchestrator` takes a symbol + date range, computes all indicators on all timeframes
- [ ] Returns `MultiTimeframeData` containing:
  - `Dictionary<CandleInterval, CandleWithIndicators[]>` for daily, weekly, monthly
  - Each `CandleWithIndicators` has: OHLCV + all computed indicator values
- [ ] Timeframe alignment: weekly/monthly indicators mapped to daily dates (forward-fill)
- [ ] Configurable indicator parameters (e.g., SMA period, RSI period) via `IndicatorConfig`
- [ ] Indicator warm-up handled: first N bars excluded from signal generation

**Technical Notes:**
- Forward-fill: Monday's daily bar uses the previous week's weekly indicators until Friday
- This is the data structure the backtesting engine iterates over
- Cache computed indicators per symbol+timeframe+daterange to avoid recomputation

**Points:** 5

---

### Phase 3: Backtesting Engine (Rewrite)

---

#### STORY-028: Strategy definition model overhaul

**Priority:** Must Have

**User Story:**
As a trader, I want to define complex strategies with multi-timeframe entry/exit conditions, multiple confirmations, and position sizing rules.

**Acceptance Criteria:**
- [ ] New `StrategyDefinition` model (replaces simple StrategyRule):
  ```
  StrategyDefinition:
    Name, Description
    EntryConditions: List<ConditionGroup>  (ALL groups must be true = AND)
      ConditionGroup:
        Timeframe: Daily|Weekly|Monthly
        Conditions: List<Condition>  (ANY condition in group = OR)
          Condition:
            Indicator: SMA|EMA|RSI|MACD|BB|ATR|Stochastic|OBV|Price
            Comparison: CrossAbove|CrossBelow|GreaterThan|LessThan|Between
            Value: decimal (or reference to another indicator)
            Period: int
    ExitConditions: same structure as EntryConditions
    StopLoss: { Type: ATR|Fixed|Support, Multiplier: decimal }
    TakeProfit: { Type: RMultiple|Fixed|Resistance, Multiplier: decimal }
    PositionSizing: { RiskPercent: decimal, MaxPositions: int }
    Filters: { MinVolume, MinPrice, MaxPrice, Sectors }
  ```
- [ ] Backward-compatible: old StrategyRule still works for simple strategies
- [ ] Validation: at least 1 entry condition, stop loss required
- [ ] API: POST /api/strategies/v2 accepts new model
- [ ] Serialized as JSON in `StrategyDefinition.RulesJson` column

**Technical Notes:**
- Don't break existing Strategy entity. Add `RulesJson` text column alongside existing Rules collection
- If `RulesJson` is populated, use new engine. If empty, fall back to legacy
- Example strategy: "Buy when Daily RSI < 30 AND Weekly EMA50 > EMA200 AND Volume > 1.5x average"

**Points:** 8

---

#### STORY-029: Backtesting engine core — event loop and order simulation

**Priority:** Must Have

**User Story:**
As a trader, I want a realistic backtesting engine that iterates bar-by-bar, simulates order fills with slippage, tracks positions, and enforces risk rules.

**Acceptance Criteria:**
- [ ] Bar-by-bar event loop: for each daily bar, evaluate strategy conditions
- [ ] Order types: Market (fill at next bar open), Limit (fill if price reached)
- [ ] Slippage model: configurable (default 0.1% of price)
- [ ] Commission model: configurable (default $1 per trade)
- [ ] Position tracking: entry price, size, unrealized P&L, stop loss, take profit
- [ ] Cash tracking: available cash decreases on buy, increases on sell
- [ ] No look-ahead bias: today's bar only uses data available up to yesterday
- [ ] Trade log: every entry/exit recorded with timestamp, price, P&L, reason
- [ ] Equity curve: daily account value (cash + positions at close)

**Technical Notes:**
- Critical: indicators must be pre-computed, engine just reads them
- Entry signal on bar N → order fills at bar N+1 open (realistic)
- Stop loss checked intra-bar: if Low ≤ stop, fill at stop price (not close)
- Take profit checked intra-bar: if High ≥ target, fill at target price
- Multi-position support: can hold multiple stocks simultaneously

**Points:** 8

---

#### STORY-030: Capital preservation enforcement in backtester

**Priority:** Must Have

**User Story:**
As a trader, I want the backtester to enforce all capital preservation rules so results reflect realistic risk management.

**Acceptance Criteria:**
- [ ] Position sizing: calculate shares = (Account × RiskPercent) / (Entry - Stop)
- [ ] Round down to whole shares (no fractional)
- [ ] Max positions limit: reject new entries if at limit
- [ ] Portfolio heat check: total risk of all open positions ≤ max heat %
- [ ] Drawdown circuit breaker: if account drops X% from peak, stop all new entries
- [ ] Recovery: resume trading after account recovers to within Y% of peak (configurable)
- [ ] No averaging down: if already long a symbol, don't add
- [ ] All rules logged in trade log with reason for skip/rejection

**Technical Notes:**
- Risk per trade = shares × (entry - stop)
- Portfolio heat = sum of all open position risks / account value
- Circuit breaker state machine: Active → Paused (drawdown hit) → Active (recovered)

**Points:** 5

---

#### STORY-031: Performance metrics calculator

**Priority:** Must Have

**User Story:**
As a trader, I want accurate performance metrics including risk-adjusted returns and S&P 500 comparison so I know if a strategy is worth trading.

**Acceptance Criteria:**
- [ ] All metrics from the metrics table computed correctly:
  - CAGR, Sharpe, Sortino, Max Drawdown, Max DD Duration, Calmar
  - Win Rate, Profit Factor, Expectancy, Average Win, Average Loss
  - Alpha, Beta vs SPY benchmark
- [ ] Monthly returns matrix (for heatmap)
- [ ] Yearly returns breakdown
- [ ] Benchmark comparison: SPY buy-and-hold return for same period
- [ ] **Red flag** if strategy CAGR < SPY CAGR or Sharpe < 1.0
- [ ] Risk-free rate configurable (default: 4.5% — current T-bill rate)

**Technical Notes:**
- Sharpe = (mean(daily_returns) - risk_free_daily) / stdev(daily_returns) × sqrt(252)
- Sortino = same but denominator uses only negative returns
- Alpha = strategy_return - (risk_free + beta × (market_return - risk_free))
- Beta = covariance(strategy_daily, spy_daily) / variance(spy_daily)
- Use 252 trading days/year for annualization

**Points:** 5

---

### Phase 4: Optimization & Walk-Forward

---

#### STORY-032: Parameter space definition and grid search

**Priority:** Must Have

**User Story:**
As a trader, I want to define parameter ranges for my strategy and find optimal values through systematic testing.

**Acceptance Criteria:**
- [ ] `ParameterSpace` model: list of parameters with min, max, step
  - Example: RSI period (10-30, step 2), SMA fast (5-20, step 5), SMA slow (30-100, step 10)
- [ ] Grid search: enumerate all combinations, run backtest for each
- [ ] Rank results by Sharpe Ratio (not raw return — avoids curve-fitting to outliers)
- [ ] Top N results stored with full metrics
- [ ] Progress tracking: X/N combinations tested
- [ ] Parallel execution within rate limits

**Technical Notes:**
- Total combinations = product of all parameter steps
- Example: 3 params with 10 steps each = 1000 combinations
- Warn user if combinations > 10,000 (suggest reducing ranges)
- Run backtests in-memory (no DB persistence for intermediate results)

**Points:** 5

---

#### STORY-033: Walk-forward analysis with overfitting detection

**Priority:** Must Have

**User Story:**
As a trader, I want walk-forward validation to ensure optimized parameters work on unseen data, preventing overfitting.

**Acceptance Criteria:**
- [ ] Walk-forward windows: split data into N periods
  - Example (5 years): 4 windows of [2yr in-sample, 6mo out-of-sample]
- [ ] For each window: optimize on in-sample → test best params on out-of-sample
- [ ] Aggregate out-of-sample results = walk-forward equity curve
- [ ] Overfitting score: (in-sample Sharpe - out-of-sample Sharpe) / in-sample Sharpe
  - < 30% = Good, 30-50% = Warning, > 50% = Overfitted
- [ ] Walk-forward efficiency: out-of-sample Sharpe / in-sample Sharpe
  - > 0.5 = acceptable, < 0.5 = likely overfitted
- [ ] Final "blessed" parameters: those that performed best across ALL out-of-sample windows
- [ ] Full result stored: per-window metrics + aggregate metrics + overfitting scores

**Technical Notes:**
- This is the gold standard for strategy validation
- Anchored walk-forward: in-sample always starts from beginning (grows each window)
- Rolling walk-forward: in-sample window slides (fixed size)
- Default: rolling with 2-year in-sample, 6-month out-of-sample

**Points:** 8

---

#### STORY-034: Backtest result storage overhaul

**Priority:** Must Have

**User Story:**
As a trader, I want full backtest results stored including equity curve, trade log, and optimization metadata so I can review and compare runs.

**Acceptance Criteria:**
- [ ] Extend BacktestResult entity:
  - `EquityCurveJson`: daily equity values (date + value)
  - `TradeLogJson`: list of all trades (entry/exit date, price, P&L, reason, holding period)
  - `MonthlyReturnsJson`: matrix of month × year returns
  - `BenchmarkReturnJson`: SPY equity curve for same period
  - `ParametersJson`: strategy parameters used
  - `WalkForwardJson`: per-window results if from optimization
  - `OverfittingScore`: decimal
  - `SpyComparison`: { strategyCagr, spyCagr, alpha, beta }
- [ ] Backtest comparison API: GET /api/backtests/compare?ids=a,b,c
- [ ] Compression: gzip equity curve + trade log before storing (can be large)

**Technical Notes:**
- Trade log for 5 years of daily trading could be 500+ trades
- Equity curve: 1260 data points (5 years × 252 days)
- Store as compressed JSON text columns

**Points:** 5

---

### Phase 5: Confidence Scoring & Stock Screening

---

#### STORY-035: Multi-confirmation signal framework

**Priority:** Must Have

**User Story:**
As a trader, I want each potential trade signal evaluated against multiple independent confirmations across timeframes so weak signals are filtered out.

**Acceptance Criteria:**
- [ ] `SignalEvaluator` takes: symbol, date, MultiTimeframeData, StrategyDefinition
- [ ] Evaluates each confirmation independently:
  - Trend alignment (higher TF trend direction matches entry direction)
  - Momentum confirmation (RSI not overbought/oversold against trade)
  - Volume confirmation (volume > 1.2× average on signal bar)
  - Volatility check (ATR within 0.5× to 2× of 50-day average)
  - MACD histogram direction aligns with trade
  - Stochastic not at extreme against trade
- [ ] Each confirmation returns true/false + weight
- [ ] Total confirmation score = sum of weighted trues / sum of all weights

**Technical Notes:**
- Confirmations are strategy-independent: they apply to ANY signal
- A strategy generates the raw signal, confirmations validate it
- This is separate from strategy conditions — it's a quality filter

**Points:** 5

---

#### STORY-036: Confidence grading and signal ranking

**Priority:** Must Have

**User Story:**
As a trader, I want each signal graded A-F with a breakdown explaining why, so I can focus on the highest-quality setups.

**Acceptance Criteria:**
- [ ] `ConfidenceGrader` takes SignalEvaluator output + R:R ratio + historical win rate
- [ ] Weighted scoring per the confidence scoring table (TF alignment 25%, confirmations 25%, volume 15%, R:R 15%, history 10%, volatility 10%)
- [ ] Grade assigned: A (90+), B (75-89), C (60-74), D (40-59), F (<40)
- [ ] `SignalReport` output: grade, score, breakdown per factor, entry/stop/target prices
- [ ] Historical tracking: grade accuracy over time (what % of A-grade signals were profitable?)
- [ ] Only A and B grades passed to screener output

**Technical Notes:**
- Historical win rate: query past trades with similar setup (same indicators triggering)
- If no historical data, use backtest win rate as proxy
- R:R ratio calculated from entry vs stop vs target prices

**Points:** 5

---

#### STORY-037: Stock screener engine

**Priority:** Must Have

**User Story:**
As a trader, I want a daily scanner that runs the optimized strategy against all stocks in my universe and surfaces ranked trade opportunities.

**Acceptance Criteria:**
- [ ] `ScreenerEngine` takes: UniverseId, StrategyDefinition (with optimized params)
- [ ] For each symbol in universe: compute indicators → evaluate entry conditions → grade signal
- [ ] Output: list of `ScreenerResult` sorted by confidence score descending
- [ ] Filters: min grade (default B), min volume, sectors, max signals per day
- [ ] `ScreenerResult`: symbol, grade, score, entry price, stop, target, R:R, confirmations breakdown
- [ ] Storage: `ScreenerRun` entity with results, queryable by date

**Technical Notes:**
- Run on latest data (today's close or yesterday if market still open)
- Compute indicators fresh for each symbol (use cached data from ingestion)
- 50 symbols × indicator computation = ~2-5 seconds total

**Points:** 5

---

#### STORY-038: Screener scheduling and API endpoints

**Priority:** Must Have

**User Story:**
As a trader, I want the screener to run automatically after market close and I want API endpoints to query results.

**Acceptance Criteria:**
- [ ] BackgroundService: runs screener daily at configurable time (default 5 PM ET)
- [ ] Uses most recently optimized strategy parameters
- [ ] API endpoints:
  - `POST /api/screener/run` — trigger manual scan
  - `GET /api/screener/results` — latest results (filterable by grade, date)
  - `GET /api/screener/results/{symbol}` — detailed signal for a symbol
  - `GET /api/screener/history` — past scan results (paged)
- [ ] Stores last 30 days of scan results

**Technical Notes:**
- Reuse DCA plan BackgroundService pattern (polling-based, since Wolverine ScheduleAsync unavailable)
- Link optimized params from walk-forward to screener config

**Points:** 5

---

#### STORY-039: Backtest-to-screener parameter pipeline

**Priority:** Should Have

**User Story:**
As a trader, I want optimized parameters from walk-forward analysis to automatically feed into the screener so I always screen with the best-known parameters.

**Acceptance Criteria:**
- [ ] `OptimizedParameterSet` entity: strategy ID, parameters, walk-forward metrics, created date
- [ ] When walk-forward completes: store "blessed" params as latest OptimizedParameterSet
- [ ] Screener loads latest OptimizedParameterSet for its strategy
- [ ] Versioning: keep last 5 parameter sets for rollback
- [ ] API: GET /api/strategies/{id}/optimized-params — current + history

**Technical Notes:**
- Simple FK chain: Strategy → OptimizedParameterSet → ScreenerConfig
- If no optimized params exist, screener uses strategy's default params

**Points:** 3

---

### Phase 6: Frontend — Backtesting & Screening UI

---

#### STORY-040: TradingView Lightweight Charts integration

**Priority:** Must Have

**User Story:**
As a trader, I want interactive candlestick charts with indicator overlays so I can visually analyze stocks and backtest results.

**Acceptance Criteria:**
- [ ] Install `lightweight-charts` package
- [ ] Reusable `<app-price-chart>` component: candlestick + volume
- [ ] Indicator overlays on price chart: SMA, EMA, Bollinger Bands (as line series)
- [ ] Separate indicator panels below chart: RSI, MACD, Stochastic
- [ ] Multi-timeframe tabs: Daily / Weekly / Monthly
- [ ] Signal markers: triangle up (buy) / triangle down (sell) on chart
- [ ] Responsive: fills container width, min-height 400px

**Technical Notes:**
- `npm install lightweight-charts`
- Use Angular wrapper: create a directive or component that manages chart lifecycle
- TradingView Lightweight Charts v4 API: `createChart()`, `addCandlestickSeries()`, `addLineSeries()`

**Points:** 8

---

#### STORY-041: Backtest configuration and execution UI

**Priority:** Must Have

**User Story:**
As a trader, I want a form to configure and run backtests with real-time progress and result visualization.

**Acceptance Criteria:**
- [ ] Strategy builder form: select indicators, set conditions, set parameters
- [ ] Parameter ranges for optimization (min/max/step inputs)
- [ ] Symbol picker + date range picker
- [ ] Capital preservation settings (risk %, max positions, max drawdown)
- [ ] "Run Backtest" button with progress indicator
- [ ] "Run Walk-Forward" button for optimization
- [ ] Results redirect to results dashboard

**Technical Notes:**
- Multi-step form: Strategy → Parameters → Risk Settings → Review → Run
- WebSocket or polling for progress updates
- Store form state in a service so user doesn't lose config on navigation

**Points:** 5

---

#### STORY-042: Backtest results dashboard

**Priority:** Must Have

**User Story:**
As a trader, I want a comprehensive results dashboard showing equity curve, metrics, trade log, and S&P 500 comparison.

**Acceptance Criteria:**
- [ ] Equity curve chart (TradingView line chart) with SPY overlay
- [ ] Key metrics cards: CAGR, Sharpe, Sortino, Max DD, Win Rate, Profit Factor
- [ ] **SPY comparison banner**: "Strategy: +42% vs SPY: +38%" (green if beating, red if not)
- [ ] Monthly returns heatmap (green/red color scale)
- [ ] Trade log table: sortable by date, P&L, holding period, entry/exit reason
- [ ] Drawdown chart (area chart showing drawdown periods)
- [ ] Walk-forward results: per-window metrics table + overfitting score badge
- [ ] Export: download trade log as CSV

**Technical Notes:**
- Install Apache ECharts for heatmap and additional charts: `npm install echarts ngx-echarts`
- Equity curve + SPY on same TradingView chart with different colors
- Color coding: green cells for positive months, red for negative

**Points:** 8

---

#### STORY-043: Screener results and signal detail view

**Priority:** Must Have

**User Story:**
As a trader, I want to see today's screener results ranked by confidence and drill into signal details with charts.

**Acceptance Criteria:**
- [ ] Screener results table: symbol, grade badge (A/B color-coded), confidence score, entry, stop, target, R:R
- [ ] Sortable by grade, score, R:R, symbol
- [ ] Filter by grade (A only, A+B), sector
- [ ] Click signal → detail view:
  - Price chart with entry/stop/target lines drawn
  - Indicator panels showing what triggered the signal
  - Confirmation breakdown (which factors scored, which didn't)
  - Historical accuracy for this setup type
- [ ] "Paper Trade" button: auto-fills order form from signal

**Technical Notes:**
- Detail view reuses `<app-price-chart>` component with signal markers
- Draw horizontal lines for entry, stop, target with labels
- Confirmation breakdown as a horizontal bar chart (scored vs total per factor)

**Points:** 5

---

#### STORY-044: Dashboard home with screener highlights and portfolio

**Priority:** Should Have

**User Story:**
As a trader, I want a home dashboard showing my portfolio, today's top signals, and recent backtest results at a glance.

**Acceptance Criteria:**
- [ ] Portfolio summary cards: total value, daily P&L, open positions count
- [ ] Top signals widget: latest A/B grade signals (top 5)
- [ ] Recent backtests: last 3 runs with key metrics
- [ ] Active DCA plans status
- [ ] Market overview: SPY daily change, VIX (if available)
- [ ] Quick actions: run screener, new backtest, place order

**Technical Notes:**
- Compose from smaller widgets, each calling its own API
- Skeleton loaders while data loads
- Auto-refresh screener signals on component init

**Points:** 5

---

## Dependency Graph

```
STORY-020 (Yahoo Finance provider)
    ↓
STORY-021 (Data ingestion + multi-TF)
    ↓
STORY-022 (Universe + SPY benchmark) ←──┐
    ↓                                    │
STORY-023 (Bulk backfill service)        │
    ↓                                    │
STORY-024 (SMA/EMA/WMA indicators)      │
    ↓                                    │
STORY-025 (RSI/MACD/Stochastic)         │
    ↓                                    │
STORY-026 (ATR/Bollinger/OBV)           │
    ↓                                    │
STORY-027 (Multi-TF orchestrator)        │
    ↓                                    │
STORY-028 (Strategy model overhaul)      │
    ↓                                    │
STORY-029 (Backtest engine core)  ───────┘
    ↓
STORY-030 (Capital preservation)
    ↓
STORY-031 (Performance metrics + SPY comparison)
    ↓
STORY-032 (Grid search optimization)
    ↓
STORY-033 (Walk-forward analysis)
    ↓
STORY-034 (Result storage overhaul)
    ↓
STORY-035 (Multi-confirmation signals)
    ↓
STORY-036 (Confidence grading A-F)
    ↓
STORY-037 (Stock screener engine)
    ↓
STORY-038 (Screener scheduling + API)
    ↓
STORY-039 (Param pipeline: optimizer → screener)

Frontend (can start after STORY-027):
STORY-040 (TradingView Charts)
    ↓
STORY-041 (Backtest config UI)     → needs STORY-028
STORY-042 (Backtest results UI)    → needs STORY-034
STORY-043 (Screener UI)            → needs STORY-038
STORY-044 (Dashboard)              → needs STORY-043
```

---

## Risks and Mitigation

**High:**
- **Yahoo Finance rate limiting or blocking** — Mitigation: aggressive caching, respect rate limits, have Alpha Vantage as fallback
- **Overfitting to historical data** — Mitigation: walk-forward validation is mandatory, overfitting score flagging
- **Backtest engine bugs producing unrealistic results** — Mitigation: verify against known strategy results, extensive unit testing

**Medium:**
- **Slow backtest execution for large parameter spaces** — Mitigation: in-memory computation, parallel where possible, warn on >10K combinations
- **Data quality issues (splits, gaps, survivorship bias)** — Mitigation: use adjusted close, handle gaps, document limitations
- **SQLite performance with large datasets** — Mitigation: proper indexes, batch inserts, consider migrating to PostgreSQL if needed

**Low:**
- **Yahoo Finance API format changes** — Mitigation: adapter pattern, easy to swap provider
- **Browser performance with large charts** — Mitigation: TradingView Lightweight Charts is designed for this

---

## Definition of Done

For a story to be considered complete:
- [ ] Code implemented and committed
- [ ] Unit tests written and passing for calculation logic (indicators, metrics, scoring)
- [ ] Integration tests for data pipeline and engine
- [ ] No look-ahead bias in backtest engine (verified by test)
- [ ] Capital preservation rules enforced (verified by test)
- [ ] API endpoints documented with example requests/responses
- [ ] Performance acceptable: full 5-year backtest < 5 seconds, 50-stock screener scan < 30 seconds

---

## Success Criteria

The system is considered successful when:

1. **Beats S&P 500**: At least one optimized strategy shows positive Alpha with Sharpe > 1.0 across walk-forward validation
2. **Capital preserved**: Max drawdown stays below 15% in all walk-forward out-of-sample windows
3. **Screening works**: Daily scanner produces actionable A/B grade signals with >55% historical accuracy
4. **Realistic results**: Backtest includes slippage, commissions, and position sizing — no fantasy returns
5. **No overfitting**: Walk-forward efficiency > 0.5 for selected strategies

---

**This plan was created using BMAD Method v6 — Phase 4 (Implementation Planning)**
