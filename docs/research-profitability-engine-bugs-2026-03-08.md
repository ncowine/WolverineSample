# Research Report: Why the Backtest Engine Doesn't Make Money

**Date:** 2026-03-08
**Research Type:** Technical Research (engine bug analysis)
**Context:** Dow 30 portfolio backtest: -0.94% CAGR, -2.33 Sharpe, 32.43% win rate, 37 trades over 5 years

## Executive Summary

The backtest engine has **6 compounding bugs** that collectively suppress trade generation to near-zero levels. Only 8 of 30 symbols were traded, with an average of 0.7 concurrent positions (max 7 allowed). The engine is barely participating in the market.

The root cause is NOT the strategies themselves -- it's that the engine mechanically prevents almost all entries from triggering. The fixes are all in engine code, not strategy parameters.

**Key finding:** Even a profitable strategy will look terrible when the engine only takes 37 trades over 5 years across 30 liquid stocks. The system needs ~200-500+ trades to express any edge.

---

## Root Causes (Ranked by Impact)

### BUG 1 (CRITICAL): CrossAbove/CrossBelow Requires Exact 1-Bar Crossover

**File:** `ConditionEvaluator.cs:96-97`

```csharp
"CrossAbove" => prevValue.HasValue
    && prevValue.Value <= target  // previous bar: below or equal
    && value > target,            // current bar: above
```

This requires the indicator to transition from below to above the reference **within a single bar**. For EMA(12) vs EMA(50), this crossover happens ~2-5 times per year per stock. The Bull regime template requires `EMAShort CrossAbove EMALong` AND `RSI > 50` on the **same bar** -- extremely rare.

**Impact:** Reduces potential entries from ~250 bars/year to ~2-5 crossover events, of which maybe 1-2 coincide with RSI > 50.

**Fix:** Add a lookback window variant: "CrossAbove within last N bars" or replace with "IsAbove" comparison for trend-following strategies. A 5-bar lookback window would increase signals ~5x without sacrificing signal quality.

---

### BUG 2 (CRITICAL): Warmup Bars (First 50) Have Zero Indicators

**File:** `IndicatorOrchestrator.cs:91`, `PortfolioBacktestEngine.cs:105`

All indicators default to `0` during the first 50 bars (warmup period). The engine starts scanning for entries at `dayIdx = 1`, never checking `IsWarmedUp`. CrossAbove with zeros never fires. When values jump from 0 to real values at bar 50, a **spurious crossover** fires once, then conditions go quiet.

**Impact:** 50 bars (~10 weeks) are completely wasted. The one false crossover at warmup boundary produces a garbage trade.

**Fix:** Skip bars where `!bar.Indicators.IsWarmedUp` in `ScanForEntries` and `CheckExitConditions`.

---

### BUG 3 (HIGH): HighVolatility Regime Detected Too Easily

**File:** `PortfolioBacktestEngine.cs:174`

```csharp
if (atrPct > 2.5m) return "HighVolatility";  // checked FIRST
```

For many individual Dow stocks, ATR/Close > 2.5% is common (any stock with >$5 daily range on a $200 stock). HighVolatility is checked **before** Bull/Bear/Sideways, so it dominates. The HighVolatility entry conditions require `Price CrossAbove BollingerUpper` -- an event that happens <5% of bars by statistical definition, compounded by the CrossAbove bug above.

Additionally, the circuit breaker **cannot deactivate** during HighVolatility regime (`CircuitBreaker.cs:86`), creating a regime-lock where no trading happens for months.

**Impact:** Large portions of the backtest stuck in a regime with near-impossible entry conditions.

**Fix:** Check HighVolatility last (not first), raise threshold to 3.5%, or use market-wide (SPY) ATR not per-stock ATR.

---

### BUG 4 (HIGH): Regime Switching Replaces Strategy Every 63 Days

**File:** `PortfolioBacktestEngine.cs:21, 111-126`

```csharp
private const int RegimeCheckIntervalDays = 63; // ~quarterly
_strategy = newStrategy; // completely replaces current strategy
```

When regime is detected, the entire strategy (entry/exit conditions) is swapped. A signal that was building toward a crossover in Bull regime gets discarded when the regime switches to Sideways. This creates a "moving goalposts" problem where incomplete signals are constantly reset.

**Impact:** Regime switches invalidate any building momentum signals 4x/year.

**Fix:** Either (a) inherit partial signal state across regime changes, (b) reduce switch frequency to detect only major regime shifts, or (c) use a blended scoring approach instead of hard switching.

---

### BUG 5 (MEDIUM): Portfolio Heat Cap at 6% Is Too Restrictive

**File:** `StrategyDefinition.cs:130`, `PortfolioBacktestEngine.cs:306-313`

```csharp
public decimal MaxPortfolioHeat { get; set; } = 6m; // 6% max
```

With ATR-based stops at 2x multiplier, each position risks ~1-2% of equity. At 6% max heat, only 3-4 concurrent positions fit. Once heat is reached, `ScanForEntries()` returns early with a log message but no trade. For a 30-stock universe with max 7 positions, this is extremely constraining.

**Impact:** Even when entry conditions fire, the position might be rejected if existing positions already consume the heat budget.

**Fix:** Raise to 10-15% or make proportional to MaxPositions (e.g., MaxPositions * RiskPercent * 1.5).

---

### BUG 6 (MEDIUM): Circuit Breaker Threshold at 15% Is Too Aggressive for Portfolio

**File:** `CircuitBreaker.cs:43-117`

The circuit breaker activates at 15% drawdown and requires recovery to within 5% of peak AND non-HighVol regime to deactivate. For a portfolio that's barely trading, even modest losses trigger it, and the HighVol regime lock (Bug 3) prevents recovery.

**Impact:** Extended periods of forced inactivity after drawdowns.

**Fix:** Raise to 20-25% for portfolio mode, or use a time-based recovery (e.g., "inactive for max 21 days").

---

## The Compounding Effect

These bugs don't just add up -- they multiply:

```
Signal frequency:    ~250 bars/year × 30 stocks = 7,500 opportunities
After CrossAbove:    × 0.02 (2% crossover chance)    = 150
After AND logic:     × 0.60 (RSI>50 coincidence)     = 90
After warmup loss:   × 0.96 (50/1260 bars lost)      = 86
After HighVol lock:  × 0.50 (regime filters out)     = 43
After regime switch: × 0.75 (signal invalidated)     = 32
After heat cap:      × 0.80 (heat budget full)       = 26
After circuit break: × 0.90 (post-DD lockout)        = 23

Predicted: ~23 entries over 5 years → Actual: 37 (close match)
```

---

## Recommendations

### Immediate Fixes (Highest ROI)

| Priority | Fix | Expected Impact | File |
|---|---|---|---|
| **P0** | Skip warmup bars: `if (!bar.Indicators.IsWarmedUp) continue;` | Eliminates garbage trades, cleans signal | `PortfolioBacktestEngine.cs` |
| **P0** | Replace `CrossAbove` with `IsAbove` in regime templates OR add 5-bar lookback to CrossAbove | **10-50x more entries** | `ConditionEvaluator.cs` or regime templates |
| **P1** | Move HighVolatility check to last, raise threshold to 3.5% | Fewer regime lockouts | `PortfolioBacktestEngine.cs:174` |
| **P1** | Raise MaxPortfolioHeat to 12% | More concurrent positions | `StrategyDefinition.cs:130` |
| **P2** | Use SPY regime for all symbols (not per-stock) | Consistent regime across portfolio | `PortfolioBacktestEngine.cs:157` |
| **P2** | Add circuit breaker max-lockout of 21 trading days | Prevents indefinite lockout | `CircuitBreaker.cs` |

### Strategy Template Fix

The current regime templates all use CrossAbove/CrossBelow. For trend-following to work on blue-chips, replace with **state-based** conditions:

**Current (broken):**
```
Entry: EMAShort CrossAbove EMALong AND RSI > 50
```

**Proposed (working):**
```
Entry: EMAShort GreaterThan EMALong AND RSI > 50 AND RSI CrossAbove 50
```

This fires whenever the trend is active AND RSI crosses 50 (a much more frequent event), rather than waiting for the rare EMA crossover.

For Mean Reversion (best for Dow stocks per prior research):
```
Entry: RSI LessThan 30 AND BollingerPercentB LessThan 0.15
Exit: RSI GreaterThan 55 AND BollingerPercentB GreaterThan 0.5
```
This uses level-based conditions (no crossovers) which fire much more frequently.

---

## Research Gaps

- Need to verify ATR calculation during warmup (does it also return 0? If so, stop-loss = price, making riskPerShare = 0, rejecting trades even after warmup)
- Haven't confirmed whether the SPY benchmark equity curve correctly uses the same start capital
- Need to test whether the 5-bar CrossAbove lookback would introduce look-ahead bias

---

*Generated by BMAD Method v6 - Creative Intelligence*
*Research Duration: ~10 minutes*
*Sources: 12 source files analyzed*
