# Research Report: Why The System Isn't Making Money & How To Fix It

**Date:** 2026-03-08
**Type:** Technical + Market Research
**Trigger:** Dow 30 portfolio backtest: -0.63% CAGR, -2.25 Sharpe, 36.84% win rate, 0.70 profit factor

---

## Executive Summary

The system has three compounding problems: (1) wrong strategy for the universe, (2) no parameter optimization despite having the infrastructure built, and (3) unrealistic cost modeling. A momentum strategy on slow-moving Dow 30 blue chips is fundamentally mismatched — academic research shows **mean reversion on large-caps has 65-75% win rates** vs the 36.84% observed. The walk-forward optimizer and grid search are fully built and tested but never wired into the production handler. Cost profiles are hardcoded to US defaults even though the user trades from a UK/GBP account.

**Key numbers from research:**
- RSI(2) mean reversion on QQQ: ~75% win rate, Sharpe ~2.85
- IBS+RSI on S&P 500 (25-year backtest): Sharpe ~2.1, 69% win rate
- Minimum 200-500 trades needed for statistical significance
- UK trader realistic costs on US stocks via IBKR: ~0.11-0.15% round-trip (manual FX) vs ~1.6% (auto FX)
- Keep free parameters to 2-4 maximum to avoid curve-fitting

---

## Research Question 1: Why is the strategy losing money?

### Root Cause: Strategy-Universe Mismatch

The auto-regime detector analyzed SPY, detected "Bull", and applied the **Momentum** template (EMA 12/50 crossover + RSI > 50) to all 30 Dow stocks. This is wrong because:

| Timeframe | Better Strategy for Blue Chips | Why |
|-----------|-------------------------------|-----|
| Short-term (< 3 months) | **Mean Reversion** | Large-caps have deep liquidity, revert from overextension quickly |
| Medium-term (3-12 months) | Momentum | Institutional flows sustain trends |
| Long-term (> 12 months) | Momentum + Fundamental | Earnings growth compounds |

The backtest is on **daily bars over 5 years** — this is short-term signal territory. Mean reversion dominates here.

**Evidence:**
- RSI(2) on QQQ: ~75% win rate, profit factor ~3, Sharpe ~2.85 ([QuantifiedStrategies](https://www.quantifiedstrategies.com/mean-reversion-strategies/))
- IBS + RSI on S&P 500 (25 years): Sharpe ~2.1, 69% win rate ([QuantifiedStrategies](https://www.quantifiedstrategies.com/sp-500-mean-reversion-using-ibs-and-rsi/))
- Bollinger Band reversion on S&P 500: ~65% win rate, 1.8:1 R/R ([Bookmap](https://bookmap.com/blog/momentum-vs-mean-reversion-which-dominates-in-a-choppy-market/))
- Dynamic regime-switching (momentum + mean-reversion combined) outperforms either alone ([Hudson & Thames](https://hudsonthames.org/dynamically-combining-mean-reversion-and-momentum-investment-strategies/))

### Secondary Cause: Only 8 of 30 Stocks Traded

EMA(12) crossing EMA(50) + RSI > 50 is too restrictive for stable blue chips. These conditions fire ~3-5x/year per stock. With 30 stocks and 5 years, you'd expect ~450-750 signals but the system only found enough conviction to trade 8 stocks, averaging 0.7 open positions. The strategy sits in cash most of the time.

### Tertiary Cause: Take-Profit Too Aggressive

R-Multiple 2.5x on a stock that moves 0.8%/day means waiting for a 4-5% move. Dow stocks rarely make that without pullbacks. Trades reverse and hit the ATR stop first.

**Confidence:** High
**Supporting sources:** 4 academic/quantitative sources

---

## Research Question 2: What does the engine get wrong technically?

### A. Cost Profile Hardcoded to US (Line 262 of RunBacktestHandler)

```csharp
var costProfile = CostProfileData.UsDefault;  // Always US, ignores user settings
```

The user's settings say `CostProfileMarket = "UK"` but the handler ignores it. For a UK trader on US stocks, costs differ:

| Component | US Default (current) | UK→US Reality (IBKR manual FX) | UK→US Reality (auto FX) |
|-----------|---------------------|-------------------------------|------------------------|
| Commission | $0.005/share | $0.0035/share ($0.35 min) | Same |
| FX spread | 0% | ~0.03% | **~0.75%** |
| Stamp duty | 0% | 0% (US stocks exempt) | 0% |
| Bid-ask spread | 0.1% | ~0.01% (Dow 30) | Same |
| **Total round-trip** | **~0.21%** | **~0.11-0.15%** | **~1.6%** |

If the user's broker uses auto-FX, costs are ~8x higher than modeled. This alone can turn a marginally profitable strategy into a loser.

### B. No Walk-Forward Validation

The GridSearchOptimizer and WalkForwardAnalyzer are fully built (parallel grid search, rolling/anchored windows, overfitting grades) but **never called from RunBacktestHandler**. Every backtest runs on the full historical period with no out-of-sample validation. Results are unreliable.

Key stat: With only ~50-100 trades in a 5-year Dow 30 backtest, you need a Sharpe > 1.0 for 95% statistical significance. The observed -2.25 Sharpe is clearly significant... as evidence the strategy doesn't work.

### C. No Parameter Optimization

The UI has parameter range fields (name, min, max, step) rendered in advanced mode step 2, but `paramRanges` is never sent in the API call. The `RunBacktestCommand` has no `ParameterRanges` field. The strategy uses whatever thresholds the template has (RSI > 50, EMA 12/50) with no ability to discover that RSI > 30 or EMA 5/20 might work better.

### D. Signal Score Has Hardcoded Magic Numbers

```
RSI contribution: max 40 points
Volume contribution: max 30 points
ATR contribution: max 30 points
Score multiplier range: [0.5, 1.0]
```

These weights are arbitrary. No calibration, no user config, no optimization.

### E. Critical Missing Features

| Feature | Impact | Status |
|---------|--------|--------|
| Walk-forward analysis | Can't detect overfitting | Built, never wired |
| Parameter grid search | Can't find optimal thresholds | Built, never wired |
| Trailing stops | Can't lock in gains | Not implemented |
| Partial profit-taking | All-or-nothing exits | Not implemented |
| Re-entry delay after stop | Whipsaw risk | Not implemented |
| Volatility-adaptive thresholds | Same RSI threshold for all stocks | Not implemented |
| Portfolio-level circuit breaker | Individual stops only | Partial (15% drawdown pause exists) |

**Confidence:** High
**Source:** Full codebase analysis of 8 engine files

---

## Research Question 3: What position sizing works for a £1,000 account?

### Kelly Criterion

```
Kelly % = W - [(1-W) / R]
```
Where W = win rate, R = avg winner / avg loser.

**Full Kelly is dangerous** — maximizes growth theoretically but assumes perfect estimates. Practitioners use:

| Fraction | Typical Allocation | Use Case |
|----------|-------------------|----------|
| Full Kelly | 33% of capital | Never for retail |
| Half Kelly | 16.5% | Experienced trader, 200+ trades |
| Quarter Kelly | **8.25%** | Recommended starting point |
| Eighth Kelly | 4.1% | First 3-6 months of live trading |

### For a £1,000 Account

At 2% risk per trade = £20 max loss. With an ATR stop of 3% on a £50 stock, position size = £20 / (3% × £50) = ~13 shares = £650. That's 65% of capital in one position — **too concentrated**.

Better: 1% risk = £10, position = ~7 shares = £350 = 35% of capital. Still concentrated but manageable with max 2-3 positions.

### The Real Problem: £1,000 Is Too Small for Diversified Equity Trading

With £1,000 and IBKR's $0.35 minimum commission per trade:
- £200 position → 0.22% commission per leg → 0.44% round-trip just in commissions
- Need strategies with >1% average gain per trade to overcome friction
- Better suited to: ETF-based mean reversion (fewer, larger positions) or index options

**Confidence:** High
**Sources:** [TradersPost](https://blog.traderspost.io/article/kelly-criterion-position-sizing-automated-trading), [QuantifiedStrategies](https://www.quantifiedstrategies.com/position-sizing-strategies/)

---

## Research Question 4: How many trades does a backtest need?

### The t-statistic relationship:

```
t-statistic = Sharpe × sqrt(N)
```

| Trades (N) | Min Sharpe for 95% significance |
|------------|-------------------------------|
| 20 | 2.0+ (barely) |
| 50 | 1.4 |
| **100** | **1.0** |
| 200 | 0.7 |
| 500 | 0.45 |

The current Dow 30 backtest likely has ~50-100 trades over 5 years with 8 symbols. That's borderline for significance even if Sharpe were positive.

### The Deflated Sharpe Ratio

If you test 50 strategy variants, a raw Sharpe of 1.5 may deflate to 0.4 after correcting for multiple testing bias. This is why walk-forward (testing on unseen data) is more reliable than cherry-picking the best parameter set.

### Regime Coverage

500 trades in 6 months (one regime) < 100 trades over 5 years (multiple regimes). The 2021-2026 window includes: post-COVID recovery, 2022 rate hike bear, 2023-24 AI bull, 2025 tariff volatility — good regime diversity.

**Confidence:** High
**Sources:** [Bailey & López de Prado](https://www.davidhbailey.com/dhbpapers/deflated-sharpe.pdf), [QuantStart](https://www.quantstart.com/articles/Sharpe-Ratio-for-Algorithmic-Trading-Performance-Measurement/)

---

## Research Question 5: What optimization method reduces overfitting most?

| Method | Evaluations Needed | Overfitting Risk | Best For |
|--------|-------------------|-----------------|----------|
| Grid Search | All combinations | **Highest** (exhaustive = finds false positives) | 2-3 parameters only |
| Random Search | ~60% of grid | Moderate | 3-5 parameters |
| **Bayesian** | ~30% of grid | **Lowest** (fewest evaluations) | 4+ parameters |

### The Parameter Robustness Test

After finding optimal params, check ±10% neighborhood. If Sharpe drops from 2.1 to 0.3 when RSI changes from 14 to 15, the strategy is curve-fitted. Robust strategies show smooth performance surfaces.

### Rule of Thumb

**Keep free parameters to 2-4 maximum.** A 2-parameter strategy is far more robust than a 7-parameter one regardless of optimization method.

The existing GridSearchOptimizer in the codebase runs parallel exhaustive search — fine for 2-3 params but risky for more. The WalkForwardAnalyzer mitigates this by validating on out-of-sample windows.

**Confidence:** High
**Sources:** [Balaena Quant](https://medium.com/balaena-quant-insights/benchmarking-optimisers-for-backtesting-part-1-2e49adb01cef), [KeyLabs AI](https://keylabs.ai/blog/hyperparameter-tuning-grid-search-random-search-and-bayesian-optimization/)

---

## Key Insights

### Insight 1: Use Mean Reversion, Not Momentum, for Dow 30
**Impact:** Critical | **Effort:** Low (template already exists)

RSI(2) mean reversion on large-cap indices has 65-75% win rates in published research. The MeanReversion template (RSI < 30 entry, RSI > 60 exit) is already defined in the codebase. Just rerun the Dow 30 backtest with it.

### Insight 2: Wire the Walk-Forward Optimizer
**Impact:** Critical | **Effort:** Medium (code exists, needs plumbing)

The GridSearchOptimizer and WalkForwardAnalyzer are fully tested but disconnected. Without OOS validation, every backtest result is unreliable. This is the single most important infrastructure gap.

### Insight 3: Fix the Cost Profile — Use BacktestConfig.CostProfileMarket
**Impact:** High | **Effort:** Low (one line change)

`RunBacktestHandler` line 262 hardcodes `CostProfileData.UsDefault`. Should read from `BacktestConfig.CostProfileMarket` which is already wired from user settings.

### Insight 4: £1,000 Is Too Small for 30-Stock Diversification
**Impact:** High | **Effort:** N/A (user constraint)

With £1,000, max 2-3 positions in ETFs or individual stocks. Don't try to trade all 30 Dow stocks — pick top 3-5 by relative strength, or use a single ETF (DIA). Reduce `MaxPositions` to 3.

### Insight 5: Per-Stock Strategy Assignment
**Impact:** High | **Effort:** Medium

Instead of one template for all 30 stocks, classify each as trending/ranging and apply different templates. The `detectStockRegime` endpoint already exists per-symbol.

### Insight 6: Limit Free Parameters to 2-4
**Impact:** Medium | **Effort:** Low (design constraint)

When the optimizer is wired, restrict search space to RSI threshold + EMA period + stop multiplier (3 params). Don't optimize everything at once.

---

## Recommended Actions

### Immediate (This Sprint)

1. **Rerun Dow 30 with MeanReversion template** — validate that the template itself works better than Momentum on this universe. Zero code changes needed.

2. **Fix cost profile line** in `RunBacktestHandler.cs:262` — use `BacktestConfig.CostProfileMarket` instead of hardcoded US default.

3. **Reduce max positions to 3-5** for a £1,000 account — concentrated positions in best signals only.

### Short-Term (Next 2 Weeks)

4. **Wire walk-forward optimizer** — connect the existing `WalkForwardAnalyzer` + `GridSearchOptimizer` + `SaveOptimizedParamsHandler` into a new `POST /api/backtests/optimize` endpoint. Separate from regular backtests to keep them fast.

5. **Send paramRanges from UI** — the fields are already rendered, just include them in the API payload.

6. **Show optimization results** — overfitting grade, OOS vs IS Sharpe, blessed parameters.

### Medium-Term (Next Month)

7. **Per-stock regime detection in portfolio mode** — assign different templates per stock based on individual regime.

8. **Trailing stops** — add to `StopLossConfig.Type` to lock in gains during rallies.

9. **Adaptive take-profit** — scale TP with stock's actual ATR instead of fixed R-multiple.

10. **Statistical significance indicator** — show trade count, t-statistic, and minimum Sharpe needed on the results page. Warn when N < 100.

---

## Research Gaps

- No live trading data to compare backtest vs reality (paper trading only)
- No analysis of tax implications (CGT on US stocks for UK residents)
- No evaluation of execution quality (IBKR fill rates vs backtest assumptions)
- Bayesian optimization not implemented (only grid search available)

---

## Sources

1. [Top Trading Bot Strategies 2026 - QuantVPS](https://www.quantvps.com/blog/trading-bot-strategies)
2. [Algo Trading for Retail - TradersPost](https://blog.traderspost.io/article/algorithmic-trading-for-retail-investors)
3. [Walk-Forward Optimization - QuantInsti](https://blog.quantinsti.com/walk-forward-optimization-introduction/)
4. [Walk-Forward vs Backtesting - Surmount](https://surmount.ai/blogs/walk-forward-analysis-vs-backtesting-pros-cons-best-practices)
5. [S&P 500 Mean Reversion - QuantifiedStrategies](https://www.quantifiedstrategies.com/sp-500-mean-reversion-using-ibs-and-rsi/)
6. [Mean Reversion Strategies - QuantifiedStrategies](https://www.quantifiedstrategies.com/mean-reversion-strategies/)
7. [Momentum vs Mean Reversion - Bookmap](https://bookmap.com/blog/momentum-vs-mean-reversion-which-dominates-in-a-choppy-market/)
8. [Dynamic Regime Switching - Hudson & Thames](https://hudsonthames.org/dynamically-combining-mean-reversion-and-momentum-investment-strategies/)
9. [Kelly Criterion - TradersPost](https://blog.traderspost.io/article/kelly-criterion-position-sizing-automated-trading)
10. [Position Sizing Strategies - QuantifiedStrategies](https://www.quantifiedstrategies.com/position-sizing-strategies/)
11. [Deflated Sharpe Ratio - Bailey & López de Prado](https://www.davidhbailey.com/dhbpapers/deflated-sharpe.pdf)
12. [Sharpe for Algo Trading - QuantStart](https://www.quantstart.com/articles/Sharpe-Ratio-for-Algorithmic-Trading-Performance-Measurement/)
13. [Benchmarking Optimisers - Balaena Quant](https://medium.com/balaena-quant-insights/benchmarking-optimisers-for-backtesting-part-1-2e49adb01cef)
14. [US Stock Costs for UK - IBKR](https://www.interactivebrokers.co.uk/en/trading/us-stock-trading-costs-for-uk-and-eu.php)
15. [Stamp Duty on Shares - Motley Fool UK](https://www.fool.co.uk/investing-basics/how-shares-are-taxed-2/stamp-duty-on-shares/)

---

*Generated by BMAD Method v6 - Creative Intelligence*
*Sources Consulted: 15*
