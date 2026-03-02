# PRD: Adaptive Trading Intelligence Engine

**Version:** 1.0
**Date:** 2026-03-02
**Author:** Product Manager (BMAD)
**Status:** Draft

---

## 1. Executive Summary

Evolve the existing Trading Assistant from a manual backtest-and-trade tool into an **Adaptive Trading Intelligence** — an AI-powered rules engine that detects market regimes, selects appropriate strategies per regime, manages risk dynamically using ML-enhanced confidence scoring, and learns from its own performance across multiple global equity markets.

The system uses Claude as a **strategist** (generating, reviewing, and refining trading rules) while keeping signal execution purely algorithmic. An ML pipeline (LightGBM/XGBoost) adds a learned confidence factor to the existing grading system, closing a genuine feedback loop: trades produce data → ML learns patterns → confidence scores improve → trade selection improves.

---

## 2. Business Objectives

| # | Objective | Measure |
|---|-----------|---------|
| O1 | Consistent risk-adjusted returns across market cycles | Walk-forward Sharpe > 1.0 on out-of-sample data |
| O2 | Adaptive to changing market conditions | Regime detection triggers strategy switch within 3 trading days |
| O3 | Self-improving over time | Feedback loop identifies strategy decay within 30 days of onset |
| O4 | Multi-market diversification | Operate independently on US + India markets at launch; extensible to LSE/Europe |
| O5 | Capital preservation first | Max drawdown < 20% in any rolling 12-month period |

---

## 3. User Personas

### Persona 1: The Quant Trader (Primary)
- Technically sophisticated, understands indicators and backtesting
- Wants a system that automates the tedious parts (screening, parameter tuning, trade review)
- Cares deeply about risk-adjusted returns, not just raw P&L
- Willing to supervise the system but doesn't want to babysit it daily

### Persona 2: The System Operator
- Monitors system health, data quality, and execution pipeline
- Needs dashboards showing regime state, strategy tournament standings, ML model health
- Wants alerts when something breaks or decays

---

## 4. Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Walk-forward Sharpe ratio | > 1.0 | Rolling 12-month out-of-sample window |
| Max drawdown (any 12-month) | < 20% | Peak-to-trough equity curve |
| Win rate on A/B-grade signals | > 55% | Closed trades with ML confidence > 0.6 |
| Strategy decay detection | < 30 days | Time from Sharpe degradation onset to alert |
| ML model lift | > 5% improvement | Comparing trade selection with/without ML factor |
| Regime classification accuracy | > 70% | Backtested against known market regimes |
| System uptime (scanning/execution) | > 99% | Background service health monitoring |

---

## 5. Functional Requirements

### Market Intelligence

#### FR-001: Market Regime Classifier

**Priority:** Must Have

**Description:**
Classify each tracked market into one of four regimes — Bull, Bear, Sideways, or High-Volatility — using a composite of technical breadth indicators. The classifier runs daily after market close and stores the current regime per market.

**Acceptance Criteria:**
- [ ] Classifies regime using: SMA slope (50/200 day), VIX level (or India VIX for Nifty), advance-decline ratio, % of stocks above 200 SMA
- [ ] Each market maintains independent regime state (US regime ≠ India regime)
- [ ] Regime transitions are logged with timestamp and triggering indicators
- [ ] Regime history is queryable for backtesting and attribution
- [ ] Backtestable: engine can replay historical regime classifications

**Dependencies:** None

---

#### FR-002: Market DNA Profiles

**Priority:** Should Have

**Description:**
Claude generates and maintains a behavioral profile for each market — characterizing whether it tends toward trending, mean-reverting, or seasonal patterns, along with typical volatility ranges, sector composition, and correlation to other tracked markets. Profiles update quarterly.

**Acceptance Criteria:**
- [ ] Each market has a stored DNA profile (JSON) with: dominant behavior, avg volatility, sector weights, cross-market correlations
- [ ] Claude analyzes 2+ years of historical data to generate initial profile
- [ ] Profiles refresh quarterly via scheduled analysis
- [ ] Profiles are inputs to strategy selection logic (FR-008)

**Dependencies:** FR-001

---

#### FR-003: Cross-Market Correlation Monitor

**Priority:** Should Have

**Description:**
Track rolling correlation between all market pairs (e.g., S&P 500 vs Nifty 50). Detect decorrelation events (correlation dropping below historical average by >1 std dev) as potential diversification opportunities or risk signals.

**Acceptance Criteria:**
- [ ] Computes 60-day rolling correlation matrix across all tracked markets
- [ ] Stores daily correlation snapshots for historical analysis
- [ ] Generates alert when correlation between two markets deviates >1σ from 1-year mean
- [ ] Correlation data available as input to portfolio allocation (FR-016)

**Dependencies:** None

---

#### FR-004: Breadth Engine

**Priority:** Must Have

**Description:**
Compute market breadth indicators for each tracked market: advance-decline line, % of stocks above 50/200 SMA, new 52-week highs vs lows, and McClellan Oscillator. These feed into regime classification and serve as standalone signals.

**Acceptance Criteria:**
- [ ] Computes all 4 breadth indicators daily for each market universe
- [ ] Values stored in time-series format, queryable for backtesting
- [ ] Breadth indicators available as conditions in StrategyDefinition (extends existing IndicatorType enum)
- [ ] Dashboard display of current breadth state per market

**Dependencies:** Requires stock universe per market (existing StockUniverse entity)

---

#### FR-005: Macro Overlay Filter

**Priority:** Could Have

**Description:**
Integrate macro-economic signals (yield curve slope, central bank rate decisions, VIX term structure) as go/no-go filters that can override or dampen trading signals. When macro conditions are unfavorable, the system reduces position sizing or halts new entries.

**Acceptance Criteria:**
- [ ] Supports configurable macro filters: yield curve (inverted = caution), VIX > threshold = reduce sizing
- [ ] Macro state logged daily alongside regime
- [ ] Filters apply at portfolio level (not per-strategy)
- [ ] Can be enabled/disabled per market

**Dependencies:** FR-001

---

### Claude-Powered Strategy Engine

#### FR-006: Strategy Generator

**Priority:** Must Have

**Description:**
Claude generates complete StrategyDefinition JSON from natural language descriptions or performance goals. The user describes intent ("momentum strategy for large-cap US stocks that works in bull markets") and Claude produces a valid v2 strategy with entry/exit conditions, stop-loss, take-profit, and position sizing.

**Acceptance Criteria:**
- [ ] Accepts natural language strategy description as input
- [ ] Outputs valid StrategyDefinition JSON (compatible with existing v2 engine)
- [ ] Claude explains its reasoning for each rule choice
- [ ] Generated strategy is automatically backtested before being saved (validation gate)
- [ ] Rejects strategies with walk-forward Sharpe < 0.5 with explanation

**Dependencies:** Existing backtest engine, existing StrategyDefinition contract

---

#### FR-007: Rule Discovery from Trade History

**Priority:** Must Have

**Description:**
Claude analyzes closed trade history to identify indicator patterns that distinguish winning trades from losing trades. Produces a report of "what conditions were present in winners but absent in losers" and recommends rule adjustments.

**Acceptance Criteria:**
- [ ] Analyzes minimum 50 closed trades to produce insights
- [ ] Captures indicator snapshot at entry for each trade (stored with trade record)
- [ ] Identifies top 5 distinguishing factors between winners and losers
- [ ] Generates recommended StrategyDefinition modifications
- [ ] Recommendations are presented to user for approval before applying

**Dependencies:** FR-024 (indicator snapshots stored with trades)

---

#### FR-008: Regime-Based Strategy Selection

**Priority:** Must Have

**Description:**
Automatically select the best-performing strategy for the current market regime. Each strategy is tagged with its optimal regime(s) based on walk-forward performance. When regime changes, the system switches to the strategy with the best out-of-sample performance for the new regime.

**Acceptance Criteria:**
- [ ] Each strategy has a regime-performance matrix (Sharpe per regime from walk-forward)
- [ ] Strategy selection triggers automatically on regime transition
- [ ] Switchover is gradual: new strategy starts at 50% allocation, scales to 100% over 5 days
- [ ] Manual override available (user can lock a strategy regardless of regime)
- [ ] Selection logic is backtestable

**Dependencies:** FR-001 (regime classifier)

---

#### FR-009: Market-Specific Playbooks

**Priority:** Should Have

**Description:**
Maintain a library of strategy templates optimized for specific markets. US playbooks emphasize momentum and sector rotation; India playbooks account for FII/DII flows, higher transaction costs, and different volatility patterns. Claude generates initial playbooks and refines them as trade data accumulates.

**Acceptance Criteria:**
- [ ] At least 3 strategy templates per market at launch (momentum, mean-reversion, breakout)
- [ ] Templates include market-specific parameters (e.g., India: wider stops for higher volatility)
- [ ] Transaction cost model is market-specific (US: ~$0.01/share; India: ~0.1% round trip)
- [ ] Claude refines playbooks quarterly based on accumulated trade performance

**Dependencies:** FR-006 (strategy generator)

---

#### FR-010: Strategy Autopsy

**Priority:** Must Have

**Description:**
After every losing month (strategy return < 0), Claude writes a detailed post-mortem analyzing what went wrong: regime mismatch, parameter drift, black swan event, or signal degradation. Includes recommended corrective actions.

**Acceptance Criteria:**
- [ ] Triggers automatically when any strategy's monthly return < 0
- [ ] Analyzes: regime during the month, indicator behavior, individual trade quality, drawdown profile
- [ ] Classifies primary loss reason from taxonomy: regime mismatch, signal degradation, black swan, position sizing error, stop-loss failure
- [ ] Produces actionable recommendations (e.g., "tighten stops", "reduce allocation", "retire strategy")
- [ ] Autopsy reports stored and queryable

**Dependencies:** FR-001, FR-024

---

### ML Confidence Pipeline

#### FR-011: Feature Store

**Priority:** Must Have

**Description:**
Maintain a feature store that captures the full indicator state at trade entry for every closed trade. Features include: all technical indicators (current values), regime label, sector, market, day-of-week, days since regime change, recent win/loss streak, and portfolio heat at time of entry.

**Acceptance Criteria:**
- [ ] Captures 30-50 features per trade at entry time
- [ ] Features include: all indicator values (RSI, MACD, SMA, etc.), regime, sector, market, volatility rank, volume rank, correlation to market, streak, portfolio heat
- [ ] Stored in structured format (JSON or flat table) linked to trade outcome
- [ ] Feature set is versioned (schema changes tracked)
- [ ] Minimum 200 trades before ML training is triggered

**Dependencies:** Existing indicator system, FR-001

---

#### FR-012: ML Trade Outcome Predictor

**Priority:** Must Have

**Description:**
Train a gradient-boosted model (LightGBM or XGBoost) to predict the probability of a profitable trade given the current feature state. Output is a probability score (0.0–1.0) representing ML confidence that this signal will result in a profitable trade.

**Acceptance Criteria:**
- [ ] Model type: LightGBM or XGBoost (interpretable, fast inference)
- [ ] Target variable: binary (profitable trade = 1, losing trade = 0)
- [ ] Walk-forward training: train on first N trades, validate on next M trades (no future leakage)
- [ ] Model outputs probability score 0.0–1.0
- [ ] Feature importance report generated with each training run
- [ ] Model performance tracked: AUC, precision, recall, calibration curve
- [ ] Minimum AUC > 0.55 required for model to be active (otherwise falls back to rules-only)

**Dependencies:** FR-011

---

#### FR-013: ML Confidence Integration

**Priority:** Must Have

**Description:**
Integrate ML model output as a 7th factor in the existing confidence grading system. The ML confidence score is weighted alongside the existing 6 factors (trend alignment, confirmations, volume, R:R, historical win rate, volatility) to produce an enhanced overall grade.

**Acceptance Criteria:**
- [ ] ML confidence added as 7th factor with configurable weight (default: 15%)
- [ ] Existing 6 factors re-weighted proportionally (sum still = 100%)
- [ ] When ML model is not yet trained (< 200 trades), system operates with original 6-factor grading
- [ ] A/B comparison available: show grade with and without ML factor
- [ ] Grade improvement tracked as ML lift metric

**Dependencies:** FR-012, existing ConfidenceGrader

---

#### FR-014: Model Retraining Pipeline

**Priority:** Must Have

**Description:**
Automatically retrain the ML model on a scheduled cadence or when triggered by performance degradation. Walk-forward retraining ensures the model stays current with recent market behavior.

**Acceptance Criteria:**
- [ ] Scheduled retraining: monthly (or every 50 new closed trades, whichever comes first)
- [ ] Walk-forward: always train on historical trades, validate on most recent window
- [ ] Model versioning: each training run produces a versioned model with metadata
- [ ] Auto-rollback: if new model AUC < previous model AUC, keep old model active
- [ ] Retraining logs and metrics stored for audit

**Dependencies:** FR-012

---

### Risk & Portfolio Intelligence

#### FR-015: Kelly-Based Position Sizing

**Priority:** Must Have

**Description:**
Calculate optimal position size using the Kelly Criterion based on rolling win rate and average win/loss ratio per strategy. Apply a fractional Kelly (default: half-Kelly) for safety, and cap at existing MaxPortfolioHeat limits.

**Acceptance Criteria:**
- [ ] Kelly fraction computed from rolling 50-trade window: f* = (W × R - L) / R where W=win rate, R=avg win/loss ratio, L=loss rate
- [ ] Default to half-Kelly (f*/2) for conservative sizing
- [ ] Position size = min(Kelly size, existing risk-per-trade limit, remaining portfolio heat capacity)
- [ ] Kelly values update after each closed trade
- [ ] Backtestable: engine can replay Kelly sizing historically
- [ ] Falls back to fixed 1% risk when insufficient trade history (< 30 trades)

**Dependencies:** None (enhances existing PositionSizingConfig)

---

#### FR-016: Correlation-Aware Allocation

**Priority:** Should Have

**Description:**
Before opening a new position, check its correlation with existing open positions. Block or reduce size if adding the position would create excessive portfolio correlation (> 0.7 avg pairwise correlation).

**Acceptance Criteria:**
- [ ] Computes 60-day rolling correlation between candidate symbol and all open positions
- [ ] Blocks new entry if avg pairwise correlation with existing portfolio > 0.7
- [ ] Reduces position size proportionally when correlation is 0.5–0.7
- [ ] Override available for user to force entry despite correlation warning
- [ ] Correlation check logged with each trade decision

**Dependencies:** FR-003

---

#### FR-017: Circuit Breaker

**Priority:** Must Have

**Description:**
Automatically halt all new trade entries when portfolio drawdown exceeds a configurable threshold. Require explicit regime re-assessment and user confirmation (or Claude analysis) before resuming.

**Acceptance Criteria:**
- [ ] Triggers when portfolio equity drops X% from peak (default: 15%)
- [ ] Immediately cancels all pending orders
- [ ] Existing positions are NOT force-closed (stops still active)
- [ ] Resume requires: (1) regime re-assessment, (2) Claude analysis of drawdown cause, (3) user approval
- [ ] Configurable thresholds per account (paper vs live can differ)
- [ ] Circuit breaker events logged with full context

**Dependencies:** FR-001

---

#### FR-018: Volatility-Targeted Sizing

**Priority:** Must Have

**Description:**
Scale position size inversely with the symbol's current ATR (Average True Range). In calm markets, take larger positions; in volatile markets, take smaller ones — targeting a constant dollar-risk per trade regardless of volatility.

**Acceptance Criteria:**
- [ ] Position size = target risk ($) / (ATR × multiplier)
- [ ] Target risk derived from Kelly or fixed risk-per-trade, whichever is active
- [ ] ATR period configurable (default: 14 days)
- [ ] Integrates with Kelly sizing (FR-015): Kelly determines risk budget, vol-targeting determines share count
- [ ] Backtestable in existing engine

**Dependencies:** Existing ATR indicator

---

#### FR-019: Geographic Risk Budget

**Priority:** Should Have

**Description:**
Enforce maximum allocation per country/region to prevent over-concentration in a single market. Default: no more than 50% of portfolio in any single country.

**Acceptance Criteria:**
- [ ] Configurable max allocation per market (default: 50%)
- [ ] Checked before each new position entry
- [ ] Dashboard shows current geographic allocation breakdown
- [ ] Allocation computed on notional value (position count × current price)

**Dependencies:** None

---

### Strategy Tournament

#### FR-020: Paper Trading Arena

**Priority:** Must Have

**Description:**
Run multiple strategy candidates simultaneously in paper trading mode. Each candidate gets a virtual paper account with identical starting capital. Performance is tracked independently and compared.

**Acceptance Criteria:**
- [ ] Support 10-20 concurrent paper strategies per market
- [ ] Each strategy gets an isolated paper account (uses existing paper trading infra)
- [ ] Daily P&L, Sharpe, drawdown tracked per candidate
- [ ] Tournament leaderboard queryable via API
- [ ] Minimum 30-day evaluation period before promotion eligibility

**Dependencies:** Existing paper trading infrastructure

---

#### FR-021: Strategy Promotion & Retirement

**Priority:** Must Have

**Description:**
Automatically promote top-performing paper strategies to live allocation and retire underperformers. Promotion criteria based on out-of-sample performance (Sharpe, drawdown, consistency). Retirement triggered by sustained underperformance or strategy decay detection.

**Acceptance Criteria:**
- [ ] Promotion criteria: 30+ days in paper, Sharpe > 1.0, max drawdown < 10%, win rate > 50%
- [ ] Promoted strategies start at 25% of target allocation, scale to 100% over 2 weeks
- [ ] Retirement criteria: Sharpe < 0.3 over rolling 60 days, or 3 consecutive losing months
- [ ] Retired strategies archived with full performance history
- [ ] User notified of all promotions and retirements with Claude's reasoning
- [ ] Manual override: user can promote/retire at any time

**Dependencies:** FR-020

---

#### FR-022: Ensemble Signal Voting

**Priority:** Should Have

**Description:**
When multiple active strategies generate signals for the same symbol, use majority voting to decide. Only execute when 2+ strategies agree on direction. Optionally weight votes by each strategy's recent Sharpe ratio.

**Acceptance Criteria:**
- [ ] Aggregates signals across all active strategies per symbol per day
- [ ] Requires configurable minimum agreement (default: 2 of 3 strategies)
- [ ] Optional Sharpe-weighted voting (higher Sharpe = more vote weight)
- [ ] Ensemble signal logged with individual strategy votes for audit
- [ ] Backtestable: can replay ensemble logic on historical strategy signals

**Dependencies:** FR-020

---

### Feedback & Learning System

#### FR-023: Automated Trade Journal with AI Review

**Priority:** Must Have

**Description:**
Every closed trade is automatically journaled with full context (entry/exit prices, indicators at entry, regime, strategy, ML confidence). Claude reviews each trade and adds qualitative analysis: what worked, what didn't, and what the system should learn.

**Acceptance Criteria:**
- [ ] Auto-journal captures: symbol, entry/exit price, duration, P&L, regime at entry/exit, strategy, grade, ML confidence, all indicator values
- [ ] Claude review runs within 24 hours of trade close
- [ ] Review classifies trade outcome: good entry/good exit, good entry/bad exit, bad entry, regime mismatch, stopped out correctly, stopped out prematurely
- [ ] Review stored alongside trade record, queryable
- [ ] Aggregate journal searchable by: strategy, regime, outcome class, market, date range

**Dependencies:** FR-011 (feature store)

---

#### FR-024: Indicator Snapshot at Entry

**Priority:** Must Have

**Description:**
Capture and store the complete indicator state (all computed indicators across all timeframes) at the moment of trade entry. This snapshot is the foundation for ML training, trade review, and pattern extraction.

**Acceptance Criteria:**
- [ ] Stores: all indicator values (RSI, MACD, SMA, EMA, Stochastic, ATR, OBV, Bollinger), regime label, breadth values, volume rank, sector
- [ ] Linked to trade record via foreign key
- [ ] Stored as compressed JSON (consistent with existing BacktestResult pattern)
- [ ] Queryable for ML feature extraction (FR-011)
- [ ] Retroactively computable for backtested trades

**Dependencies:** Existing indicator system

---

#### FR-025: Strategy Decay Monitor

**Priority:** Must Have

**Description:**
Continuously monitor each active strategy's rolling performance metrics. Detect when a strategy's edge is eroding (Sharpe declining, win rate dropping, drawdowns increasing) and alert before it becomes a significant loss.

**Acceptance Criteria:**
- [ ] Tracks rolling 30/60/90-day Sharpe, win rate, avg trade P&L, max drawdown per strategy
- [ ] Decay alert when: rolling 60-day Sharpe drops below 50% of historical average
- [ ] Severe decay alert when: rolling 30-day Sharpe goes negative
- [ ] Alerts include Claude analysis of probable cause (regime shift, market structure change, parameter drift)
- [ ] Decay detection backtestable against historical strategy performance

**Dependencies:** FR-001

---

#### FR-026: Mistake Taxonomy & Pattern Extraction

**Priority:** Should Have

**Description:**
Classify every losing trade into a mistake taxonomy and periodically extract patterns. After every 50 trades, Claude runs pattern analysis to identify systematic mistakes and recommend corrective rules.

**Acceptance Criteria:**
- [ ] Taxonomy categories: bad entry signal, bad timing (too early/late), regime mismatch, stop too tight, stop too loose, position too large, correlated loss, black swan/news
- [ ] Classification is automated (rule-based heuristics) with Claude override for ambiguous cases
- [ ] Pattern report generated every 50 closed trades
- [ ] Report identifies: most common mistake type, which market/regime produces most mistakes, recommendations
- [ ] Recommendations feed into FR-007 (rule discovery)

**Dependencies:** FR-023, FR-024

---

#### FR-027: Monthly Performance Attribution

**Priority:** Should Have

**Description:**
Break monthly returns into components: strategy alpha, market beta, regime contribution, and residual (luck/noise). Helps distinguish between "system is working" and "market went up and we happened to be long."

**Acceptance Criteria:**
- [ ] Decomposes monthly return into: alpha (strategy edge), beta (market exposure), regime (correct regime positioning), residual
- [ ] Uses benchmark (SPY for US, Nifty 50 index for India) for beta calculation
- [ ] Monthly attribution report generated automatically
- [ ] Rolling 12-month attribution summary available
- [ ] Claude narrative explaining what drove returns and what was luck

**Dependencies:** FR-001, existing benchmark data

---

### Execution & Operations

#### FR-028: Realistic Cost Model

**Priority:** Must Have

**Description:**
Apply market-specific transaction costs to all backtests and paper trades. US: commission per share + SEC fee; India: brokerage + STT + exchange fees + GST. Costs must be accurate enough that backtest results are realistic.

**Acceptance Criteria:**
- [ ] Cost profiles configurable per market: commissions, exchange fees, taxes, spread estimate
- [ ] Default profiles: US ($0.005/share + 0.1% spread), India (0.03% brokerage + 0.025% STT + 0.05% spread)
- [ ] Applied in backtest engine (deducted from each trade P&L)
- [ ] Applied in paper trading (deducted from paper account)
- [ ] Cost impact shown in backtest results (gross vs net returns)

**Dependencies:** None (enhances existing backtest engine)

---

#### FR-029: Data Quality Monitor

**Priority:** Must Have

**Description:**
Detect and alert on data quality issues: missing candles, price anomalies (gaps > 20% without news), stale data (no update in 2+ trading days), and unadjusted stock splits.

**Acceptance Criteria:**
- [ ] Checks run daily after data ingestion
- [ ] Detects: missing trading days, price gaps > 20%, zero volume days, stale feeds
- [ ] Alerts surfaced in dashboard and logged
- [ ] Affected symbols flagged — strategies skip flagged symbols until data is clean
- [ ] Historical data quality report available per symbol

**Dependencies:** None

---

#### FR-030: Gradual Capital Deployment

**Priority:** Must Have

**Description:**
New strategies (freshly promoted from paper) start with reduced capital allocation and scale up as they prove themselves in live conditions. Prevents a newly promoted strategy from immediately taking full-size positions.

**Acceptance Criteria:**
- [ ] Promoted strategy starts at 25% of target allocation
- [ ] Scales to 50% after 10 profitable trades or 2 weeks (whichever is later)
- [ ] Scales to 100% after 25 profitable trades or 4 weeks
- [ ] Immediate scale-down to 25% if drawdown exceeds 5% during ramp-up
- [ ] Scaling schedule configurable per strategy

**Dependencies:** FR-021

---

## 6. Non-Functional Requirements

### NFR-001: Backtest Performance

**Priority:** Must Have

**Description:**
Backtest engine must handle multi-year runs across large universes efficiently enough for walk-forward optimization and strategy tournaments.

**Acceptance Criteria:**
- [ ] Single-symbol 5-year backtest completes in < 5 seconds
- [ ] 500-symbol universe screening completes in < 60 seconds
- [ ] Walk-forward optimization (5 windows × 100 parameter combos) completes in < 10 minutes
- [ ] Tournament evaluation (20 strategies × daily) completes in < 5 minutes

**Rationale:** Grid search and walk-forward are compute-intensive; slow backtests block the feedback loop.

---

### NFR-002: ML Inference Latency

**Priority:** Must Have

**Description:**
ML model inference must be fast enough to score all screener signals within the daily processing window (post-market close).

**Acceptance Criteria:**
- [ ] Single prediction < 10ms
- [ ] Batch scoring of 500 signals < 2 seconds
- [ ] Model loading from disk < 1 second

**Rationale:** ML scoring is in the critical path of daily signal generation.

---

### NFR-003: Data Integrity

**Priority:** Must Have

**Description:**
All trade decisions, indicator snapshots, regime labels, and ML scores must be immutably recorded for audit and learning.

**Acceptance Criteria:**
- [ ] Trade journal entries are append-only (no edits/deletes)
- [ ] Indicator snapshots stored with cryptographic hash for tamper detection
- [ ] All regime transitions logged with triggering data
- [ ] ML model versions and predictions linkable to trade outcomes

**Rationale:** The feedback loop is only as good as its data integrity. Corrupted trade history ruins ML training.

---

### NFR-004: System Reliability

**Priority:** Must Have

**Description:**
Background services (screener, tournament, regime classifier, ML pipeline) must be resilient to transient failures.

**Acceptance Criteria:**
- [ ] All background services implement retry with exponential backoff (max 3 retries)
- [ ] Failed service runs logged with full error context
- [ ] Health check endpoint reports status of all background services
- [ ] No silent failures: every error produces an alert

**Rationale:** A missed screener run or failed regime update could lead to stale signals and bad trades.

---

### NFR-005: Security — API & Trading Authorization

**Priority:** Must Have

**Description:**
All trading operations require authenticated, authorized access. Strategy promotion to live requires elevated confirmation.

**Acceptance Criteria:**
- [ ] All endpoints require JWT authentication (existing)
- [ ] Strategy promotion to live requires explicit user confirmation (not automated)
- [ ] Claude API keys stored securely (environment variables, not in code or DB)
- [ ] ML model files stored in secure location with access logging

**Rationale:** A bug in automation should never be able to execute unauthorized live trades.

---

### NFR-006: Extensibility — New Markets

**Priority:** Should Have

**Description:**
Adding a new market (e.g., LSE, European exchanges) should require only configuration, not code changes.

**Acceptance Criteria:**
- [ ] Market definition is config-driven: exchange name, trading hours, currency, cost model, data source
- [ ] Regime classifier parameters configurable per market
- [ ] Strategy playbooks are market-tagged, new market gets default templates
- [ ] Adding a market requires < 1 day of effort

**Rationale:** Multi-market is the long-term goal; architecture must support it from day one.

---

### NFR-007: ML Model Governance

**Priority:** Must Have

**Description:**
ML models must be versioned, auditable, and safely deployable with automatic rollback.

**Acceptance Criteria:**
- [ ] Each model version stored with: training date, feature set, hyperparameters, validation metrics, training data hash
- [ ] Maximum one active model per market at any time
- [ ] Auto-rollback if new model underperforms previous version on holdout set
- [ ] Model drift detection: alert when feature distributions shift significantly from training data

**Rationale:** Unmonitored ML models degrade silently and can make increasingly bad predictions.

---

### NFR-008: Observability

**Priority:** Should Have

**Description:**
System provides comprehensive observability into all autonomous decision-making processes.

**Acceptance Criteria:**
- [ ] Dashboard shows: current regime per market, active strategies, tournament standings, ML model health, recent trades, portfolio state
- [ ] Every autonomous decision (regime change, strategy switch, trade entry/exit, promotion/retirement) logged with reasoning
- [ ] Daily summary report: trades taken, P&L, regime states, alerts, ML scores

**Rationale:** An autonomous system that you can't observe is an autonomous system you can't trust.

---

## 7. Epics

### EPIC-001: Market Intelligence Layer

**Description:**
Build the market regime classifier, breadth engine, cross-market correlation monitor, and macro overlay — the "eyes" of the system that understand what environment we're trading in.

**Functional Requirements:**
- FR-001 (Regime Classifier)
- FR-002 (Market DNA Profiles)
- FR-003 (Cross-Market Correlation)
- FR-004 (Breadth Engine)
- FR-005 (Macro Overlay)

**Story Count Estimate:** 8-10

**Priority:** Must Have

**Business Value:**
Everything depends on knowing what market regime you're in. This is the foundation that all other intelligence builds upon. Without this, strategy selection is random.

---

### EPIC-002: Claude Strategy Engine

**Description:**
Claude-powered strategy generation, regime-based selection, market-specific playbooks, and performance autopsy. Claude operates as the "coach" — building strategies, selecting the right one for conditions, and diagnosing failures.

**Functional Requirements:**
- FR-006 (Strategy Generator)
- FR-007 (Rule Discovery)
- FR-008 (Regime-Based Strategy Selection)
- FR-009 (Market-Specific Playbooks)
- FR-010 (Strategy Autopsy)

**Story Count Estimate:** 8-10

**Priority:** Must Have

**Business Value:**
This is the core intelligence that differentiates the system from a static rule engine. Claude's ability to reason about strategy design and diagnose failures is the primary competitive advantage.

---

### EPIC-003: ML Confidence Pipeline

**Description:**
Feature store, trade outcome prediction model, integration with confidence grading, and automated retraining pipeline. The "learning brain" that gets better with every trade.

**Functional Requirements:**
- FR-011 (Feature Store)
- FR-012 (ML Trade Outcome Predictor)
- FR-013 (ML Confidence Integration)
- FR-014 (Model Retraining Pipeline)

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Closes the feedback loop. Without ML learning from trade outcomes, the system never improves beyond its initial rules. This is the moat — the system gets smarter over time.

---

### EPIC-004: Risk & Portfolio Intelligence

**Description:**
Advanced position sizing (Kelly, volatility-targeting), correlation-aware allocation, circuit breaker, and geographic risk budgets. The "risk manager" that protects capital.

**Functional Requirements:**
- FR-015 (Kelly-Based Position Sizing)
- FR-016 (Correlation-Aware Allocation)
- FR-017 (Circuit Breaker)
- FR-018 (Volatility-Targeted Sizing)
- FR-019 (Geographic Risk Budget)

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Position sizing drives more P&L than signal quality. This is the highest-ROI upgrade — can double risk-adjusted returns with the same signals. Capital preservation prevents catastrophic failure.

---

### EPIC-005: Strategy Tournament

**Description:**
Paper trading arena running multiple strategies concurrently, automatic promotion of winners to live allocation, retirement of losers, and ensemble signal voting. The "natural selection" layer.

**Functional Requirements:**
- FR-020 (Paper Trading Arena)
- FR-021 (Strategy Promotion & Retirement)
- FR-022 (Ensemble Signal Voting)

**Story Count Estimate:** 5-7

**Priority:** Must Have

**Business Value:**
Instead of betting on one strategy, run a portfolio of strategies and let performance determine allocation. This is inherently anti-fragile and is how professional funds operate.

---

### EPIC-006: Feedback & Learning System

**Description:**
Automated trade journaling with Claude review, strategy decay detection, mistake taxonomy, pattern extraction, and performance attribution. The "post-game analysis" that drives continuous improvement.

**Functional Requirements:**
- FR-023 (Trade Journal AI Review)
- FR-024 (Indicator Snapshot at Entry)
- FR-025 (Strategy Decay Monitor)
- FR-026 (Mistake Taxonomy)
- FR-027 (Monthly Performance Attribution)

**Story Count Estimate:** 7-9

**Priority:** Must Have

**Business Value:**
Every strategy decays. The system that detects decay first and adapts wins. Claude reviewing every trade creates institutional knowledge that compounds over time.

---

### EPIC-007: Execution & Operations

**Description:**
Realistic cost modeling, data quality monitoring, gradual capital deployment, and operational infrastructure. The "plumbing" that ensures the smart system operates reliably.

**Functional Requirements:**
- FR-028 (Realistic Cost Model)
- FR-029 (Data Quality Monitor)
- FR-030 (Gradual Capital Deployment)

**Story Count Estimate:** 4-5

**Priority:** Must Have

**Business Value:**
A brilliant strategy with bad data, unrealistic cost assumptions, or fragile infrastructure will lose money in practice. This epic ensures the gap between backtest and reality is minimized.

---

## 8. Traceability Matrix

| Epic | Name | FRs | Story Est. | Priority |
|------|------|-----|------------|----------|
| EPIC-001 | Market Intelligence Layer | FR-001–005 | 8-10 | Must Have |
| EPIC-002 | Claude Strategy Engine | FR-006–010 | 8-10 | Must Have |
| EPIC-003 | ML Confidence Pipeline | FR-011–014 | 6-8 | Must Have |
| EPIC-004 | Risk & Portfolio Intelligence | FR-015–019 | 6-8 | Must Have |
| EPIC-005 | Strategy Tournament | FR-020–022 | 5-7 | Must Have |
| EPIC-006 | Feedback & Learning System | FR-023–027 | 7-9 | Must Have |
| EPIC-007 | Execution & Operations | FR-028–030 | 4-5 | Must Have |

**Total Estimated Stories: 44-57**

---

## 9. Prioritization Summary

| Priority | FRs | NFRs | Total |
|----------|-----|------|-------|
| Must Have | 21 | 6 | 27 |
| Should Have | 7 | 2 | 9 |
| Could Have | 2 | 0 | 2 |
| **Total** | **30** | **8** | **38** |

---

## 10. Key User Flows

### Flow 1: Daily Autonomous Cycle
```
Market Close → Data Ingestion → Regime Classification → Breadth Update
    → Strategy Selection (per regime) → Screener Run (per active strategy)
    → ML Confidence Scoring → Confidence Grading (7 factors)
    → Filter: only A/B grade signals → Portfolio Risk Check
    → Generate Orders (paper or live) → Journal Entry
```

### Flow 2: Strategy Lifecycle
```
Claude Generates Strategy → Backtest Validation Gate → Walk-Forward Test
    → If passes: Enter Tournament (paper) → 30-day evaluation
    → If top performer: Promote to Live (25% → 50% → 100%)
    → Ongoing: Decay monitoring → If decaying: Claude Autopsy → Adapt or Retire
```

### Flow 3: ML Learning Loop
```
Trade Closes → Indicator Snapshot Stored → Feature Store Updated
    → Every 50 trades: ML Retrain → Validate on holdout
    → If AUC improved: Deploy new model → Update confidence weights
    → If AUC degraded: Keep old model, investigate feature drift
```

---

## 11. Dependencies

### Internal Dependencies
- Existing backtest engine (v2 StrategyDefinition, ConditionEvaluator, PerformanceCalculator)
- Existing screener with confidence grading (ScreenerEngine, ConfidenceGrader)
- Existing paper trading infrastructure (Account, Order, Position, Portfolio)
- Existing indicator system (IndicatorOrchestrator, 11 indicators, multi-timeframe)
- Existing market data pipeline (PriceCandle, data ingestion)

### External Dependencies
- **Claude API** — Strategy generation, trade review, autopsy reports, rule discovery
- **Market Data Provider** — Reliable daily OHLCV data for US, India, (later UK/Europe)
- **ML Runtime** — Python (ML.NET, or ONNX for .NET-native inference) for LightGBM/XGBoost
- **VIX / India VIX Data** — Required for regime classification

---

## 12. Assumptions

1. Claude API is available with sufficient rate limits for daily batch analysis (strategy review, trade journal, autopsy)
2. Historical market data (5+ years) is obtainable for all target markets at daily granularity
3. Paper trading is sufficient for strategy validation before any live deployment
4. User will manually configure broker integration for actual live trading (out of scope for this PRD)
5. ML model training can run on the same machine as the trading system (no separate ML infra needed initially)
6. VIX and India VIX data are accessible through the same data pipeline as equity prices

---

## 13. Out of Scope

- **Live broker integration** — System produces signals and paper trades; actual order execution to a broker is a future phase
- **Cryptocurrency markets** — Explicitly excluded per user direction; future expansion
- **High-frequency / intraday trading** — System operates on daily bars; sub-daily timeframes are out of scope
- **Social/sentiment analysis** — News and social media sentiment are not included in this phase
- **Mobile application** — Web-based Angular client only
- **Multi-user / SaaS** — Single-user system; multi-tenancy is not a goal
- **Real-time streaming data** — System operates on end-of-day data, not tick-by-tick

---

## 14. Open Questions

| # | Question | Impact | Owner |
|---|----------|--------|-------|
| Q1 | Which ML runtime for .NET? ML.NET (native) vs Python sidecar vs ONNX export? | EPIC-003 architecture | Architect |
| Q2 | What market data provider for India (NSE)? Yahoo Finance has gaps for Indian stocks | EPIC-001 data quality | Architect |
| Q3 | Should Claude analysis be synchronous (blocking daily pipeline) or async (results available next day)? | EPIC-002, EPIC-006 latency | Product + Architect |
| Q4 | How to handle stock splits/dividends in historical data across markets? | EPIC-007 data integrity | Architect |
| Q5 | Should regime classification use fixed thresholds or adaptive thresholds per market? | EPIC-001 accuracy | Architect |

---

## 15. Recommended Phased Delivery

| Phase | Epics | Focus | Estimated Stories |
|-------|-------|-------|-------------------|
| **Phase 1** | EPIC-001 + EPIC-004 + EPIC-007 | Market Intelligence + Risk Management + Operations | 18-23 |
| **Phase 2** | EPIC-002 + EPIC-005 | Claude Strategy Engine + Tournament | 13-17 |
| **Phase 3** | EPIC-003 + EPIC-006 | ML Pipeline + Feedback Loop | 13-17 |

**Rationale:**
- Phase 1 builds the foundation (know your regime, manage your risk, trust your data)
- Phase 2 adds the intelligence (Claude generates and selects strategies, tournament validates)
- Phase 3 closes the loop (ML learns from trades, feedback drives improvement)

---

*Generated by BMAD Method v6 — Product Manager*
*PRD Session: 2026-03-02*
