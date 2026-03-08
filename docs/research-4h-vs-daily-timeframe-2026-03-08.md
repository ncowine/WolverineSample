# Research Report: 4H vs Daily Timeframe for Backtesting

**Date:** 2026-03-08
**Research Type:** Technical + Trading Strategy
**Context:** Trade log shows 52 trades held 2-90 days, mostly losses, blocking capital from other setups

---

## Executive Summary

Switching to 4H timeframe **sounds appealing but is impractical** for this system due to a hard data constraint: Yahoo Finance only provides intraday data for the last 60 days. Your backtest spans 2021-2024 (3+ years), making 4H backtesting impossible without a paid data provider. The real problem isn't the timeframe — it's that trades are held too long with no dynamic exit management. Better solutions exist within the daily timeframe.

**Verdict: Don't switch to 4H. Fix the holding period and exit logic instead.**

---

## The Problem (from trade log)

| Metric | Value |
|--------|-------|
| Total trades | 52 |
| Avg holding period | ~35 days |
| Trades held 50+ days | 8 (15%) |
| Longest hold | 90 days (DIS) |
| Win rate | ~15% (mostly ExitSignal losses) |
| Most common exit | ExitSignal (not StopLoss or TakeProfit) |

**Key observation:** Only 2 trades hit TakeProfit. Zero trades hit StopLoss. Almost everything exits via ExitSignal after extended holding, suggesting:
1. SL is too wide (ATR-based, ~13% below entry in the WMT case)
2. TP is too ambitious (R-multiple of 2.5x, ~32% above entry)
3. Trades drift sideways for weeks, tying up capital

---

## Why 4H Won't Work (Data Constraint)

### Yahoo Finance Intraday Limitations

| Interval | Max Historical Data |
|----------|-------------------|
| 1m | 7 days |
| 5m, 15m, 30m | 60 days |
| 60m / 1h | 60 days |
| 1d (daily) | 20+ years |

- **No native 4H interval** — would need to aggregate from 1H bars
- **Only 60 days of 1H data available** — can't backtest over years
- Your backtest covers **Jul 2021 - Mar 2024** (2.7 years) — impossible with Yahoo Finance intraday data
- Paid alternatives (Polygon.io, Alpha Vantage premium, Interactive Brokers) cost $30-200/mo

### Engine Refactoring Cost

Even with data, the PortfolioBacktestEngine has **6+ hardcoded daily assumptions**:
- Main loop iterates by date, not by bar
- HoldingDays uses `(exit - entry).Days`
- Regime check interval = 21 days (hardcoded)
- Indicator parameters tuned for daily (RSI 14 = 14 days, not 14 4H-bars)
- Data loading filters by `CandleInterval.Daily`
- ConditionEvaluator only supports Daily/Weekly/Monthly timeframes

**Estimated effort:** 3-5 days of refactoring across 8+ files

---

## What Would Actually Fix the Problem

### Fix 1: Tighter Max Holding Days (Quick Win)

Current: trades can drift for 90 days. The WMT trades average ~40 days.

**Recommendation:** Set `MaxHoldingDays = 15-20` for swing trades on daily charts.

**Impact:** Frees capital ~3x faster. A 20-day max hold means capital turns over 12x/year vs 4x/year with 60-day holds.

### Fix 2: Trailing Stop Loss

Current: static SL set at entry, never adjusted. Price can rally 10%, pull back 8%, and still not hit TP or SL.

**Recommendation:** Implement a trailing stop that locks in profits:
- After 2R profit (price moved 2x risk amount), trail stop to breakeven
- After 3R, trail stop to 1R profit
- Use ATR-based trailing (e.g., 2x ATR below recent high)

**Impact:** Captures partial moves instead of round-tripping to ExitSignal.

### Fix 3: Time-Based Exit Scaling

If a trade hasn't moved significantly after N days, reduce position or exit early.

**Recommendation:**
- Day 10: If P&L < +1%, tighten stop to 50% of original risk
- Day 20: If P&L < +3%, exit at market

**Impact:** Stops capital from being tied up in dead trades.

### Fix 4: Tighter SL/TP Ratios

Current WMT example: Entry $46.68, SL $40.60 (13% risk), TP $61.88 (32% reward).
- 13% risk on a single trade is huge for a $44-50 range stock
- TP at $61 requires a 32% move — WMT has ~15% annual returns

**Recommendation:**
- SL: 2x ATR or 5% max (whichever is tighter)
- TP: 1.5-2x R-multiple (not 2.5x)
- This means more trades hit TP, fewer drift to ExitSignal

### Fix 5: Multi-Timeframe Confirmation (Use What You Have)

The engine already supports Weekly/Monthly as higher timeframes. Instead of switching base to 4H:
- **Enter on daily** when Weekly trend aligns
- **Exit on daily** with tighter conditions
- This gives you the "confirmation" benefit of multi-timeframe without intraday data

---

## 4H vs Daily: General Trading Research

| Factor | Daily | 4H |
|--------|-------|----|
| Signal quality | Higher (less noise) | More noise, more false breakouts |
| Trade frequency | 1-3 setups/week | 3-8 setups/week |
| Holding period | 5-60 days | 1-14 days |
| Capital turnover | Lower | Higher |
| Monitoring | 2-4 checks/day | 6-12 checks/day |
| False signals | Fewer | More |
| Indicator reliability | RSI/EMA well-calibrated | Need parameter adjustment |
| Data availability | 20+ years (free) | 60 days max (free) |

**Key insight from research:** Many traders use daily for bias + 4H for entry timing. This hybrid approach gets the best of both, but requires intraday data which you don't have.

---

## Recommendations (Prioritized)

### Immediate (this sprint)
1. **Reduce MaxHoldingDays to 20** — single config change, huge capital efficiency gain
2. **Tighten default SL to 5% max** — prevents outsized risk on low-vol stocks

### Short-term (next sprint)
3. **Implement trailing stop** — ATR-based, activated after 1.5R profit
4. **Add time-decay exit** — progressively tighten stops after day 10

### Long-term (if needed)
5. **Multi-timeframe entry confirmation** — daily entry only when weekly trend agrees
6. **4H support** — only worth it if you switch to a paid data provider (Polygon.io at $30/mo)

---

## Sources

- [Yahoo Finance API Documentation - QuantVPS](https://www.quantvps.com/blog/yahoo-finance-api-documentation)
- [yfinance GitHub - Data limitations](https://github.com/ranaroussi/yfinance)
- [Best Timeframe for Swing Trading - Wall Street Zen](https://www.wallstreetzen.com/blog/best-timeframe-for-swing-trading/)
- [4H or Daily Timeframe - Daily Price Action](https://dailypriceaction.com/blog/4-hour-or-daily-time-frame/)
- [4H Swing Trading Strategies - TradersUnion](https://tradersunion.com/interesting-articles/swing-trading-main-strategies-and-rules/4-hour-timeframe/)
- [Freqtrade Multi-Timeframe Backtesting - DEV Community](https://dev.to/henry_lin_3ac6363747f45b4/lesson-7-freqtrade-multi-timeframe-backtesting-4h5b)
- [Daily vs 4H Profitability - Quora Discussion](https://www.quora.com/Is-the-daily-time-frame-more-profitable-than-the-4-hour-time-frame-My-system-is-an-indicator-based-system-but-I-seem-to-get-more-profits-on-the-4-hour-than-the-daily-in-the-same-period-of-backtesting)

---

*Generated by BMAD Method v6 - Creative Intelligence*
