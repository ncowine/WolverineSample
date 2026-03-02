# Sprint Plan: Adaptive Trading Intelligence

**Date:** 2026-03-02
**Project Level:** 4
**Total Stories:** 36
**Total Points:** 167
**Planned Sprints:** 7 (Sprints 10–16)
**Phases:** 3 (Intelligence + Risk → Claude + Tournament → ML + Feedback)

**PRD:** `docs/prd-adaptive-trading-intelligence-2026-03-02.md`
**Architecture:** `docs/architecture-adaptive-trading-intelligence-2026-03-02.md`

---

## Executive Summary

Build the Adaptive Trading Intelligence engine on top of the existing quantitative trading system. The plan follows the 3-phase delivery from the PRD:

- **Phase 1 (Sprints 10–12):** Market Intelligence + Risk Management + Operations — the eyes and the shield
- **Phase 2 (Sprints 13–14):** Claude Strategy Engine + Strategy Tournament — the brain
- **Phase 3 (Sprints 15–16):** ML Pipeline + Feedback Loop — the learning system

Each phase is independently valuable. Phase 1 alone transforms the system from manual to regime-aware with professional risk management. Phase 2 adds Claude-powered intelligence. Phase 3 closes the feedback loop with ML.

**Key Metrics:**
- Total Stories: 36
- Total Points: 167
- Sprints: 7 (biweekly)
- Team Capacity: 30 pts/sprint (historical velocity: 23 pts/sprint)
- Estimated Completion: Sprint 16

---

## Story Inventory

### EPIC-001: Market Intelligence Layer

#### STORY-045: IntelligenceDbContext & base domain entities (5 pts)

**Epic:** Market Intelligence | **Priority:** Must Have

**User Story:**
As a developer, I want the 4th bounded context (Intelligence) with all base entities, so that regime, tournament, ML, and feedback data has a clean home.

**Acceptance Criteria:**
- [ ] `IntelligenceDbContext` registered in DI with `intelligence.db` SQLite connection
- [ ] Domain entities created: `MarketRegime`, `RegimeTransition`, `MarketProfile`, `BreadthSnapshot`, `CorrelationSnapshot`, `CostProfile`, `PipelineRunLog`
- [ ] Enums created: `RegimeType` (Bull, Bear, Sideways, HighVolatility), `TournamentStatus`, `OutcomeClass`, `MistakeType`, `DecayAlertType`
- [ ] EF migrations generated and applied
- [ ] Indexes per architecture doc (MarketCode lookups, filtered unique indexes)

**Technical Notes:**
Follow existing DbContext pattern (see `TradingDbContext`). No interceptors needed initially. Decimal precision: (18,4) for indicators, (18,2) for money.

**Dependencies:** None — foundation for all other intelligence stories.

---

#### STORY-046: Breadth Engine (5 pts)

**Epic:** Market Intelligence | **Priority:** Must Have

**User Story:**
As a trader, I want market breadth indicators computed daily for each universe, so that I can gauge overall market health beyond individual stocks.

**Acceptance Criteria:**
- [ ] `BreadthCalculator` computes: advance-decline ratio, % stocks above 200 SMA, % stocks above 50 SMA, new 52-week highs vs lows
- [ ] Reads all PriceCandles for a StockUniverse, computes SMA per stock
- [ ] Results stored as `BreadthSnapshot` per market per day
- [ ] Handles edge cases: stocks with insufficient history, empty universe
- [ ] Unit tests for each breadth indicator with synthetic data

**Technical Notes:**
Static utility class in `Application/Intelligence/`. Uses existing `SmaCalculator` for per-stock SMA computation. Reads from MarketDataDbContext, writes to IntelligenceDbContext.

**Dependencies:** STORY-045

---

#### STORY-047: Market Regime Classifier (5 pts)

**Epic:** Market Intelligence | **Priority:** Must Have

**User Story:**
As a trader, I want each market automatically classified into a regime (Bull/Bear/Sideways/HighVol) daily, so that strategies can adapt to market conditions.

**Acceptance Criteria:**
- [ ] `RegimeClassifier` implements classification logic: VIX > threshold → HighVol; SMA slopes + breadth → Bull/Bear/Sideways
- [ ] Thresholds read from `MarketProfile` (config-driven, not hardcoded)
- [ ] Default thresholds: US (highVol=30, bullBreadth=60%, bearBreadth=40%), India (highVol=25, bullBreadth=55%, bearBreadth=35%)
- [ ] Stores `MarketRegime` with current state + confidence score
- [ ] Logs `RegimeTransition` on every regime change with triggering indicators
- [ ] Publishes `RegimeChanged` Wolverine event on transitions
- [ ] `ClassifyRegimeCommand` handler callable via `IMessageBus`
- [ ] Historical regime classification backtestable (pure function with date input)
- [ ] Unit tests covering all 4 regime classifications + transitions

**Technical Notes:**
Handler: `ClassifyRegimeHandler.HandleAsync(ClassifyRegimeCommand, MarketDataDbContext, IntelligenceDbContext)`. Returns `(RegimeChanged?, MarketRegimeDto)` tuple — cascading event only if transition occurred.

**Dependencies:** STORY-045, STORY-046

---

#### STORY-048: Cross-Market Correlation Monitor (3 pts)

**Epic:** Market Intelligence | **Priority:** Should Have

**User Story:**
As a trader, I want rolling correlation between all tracked markets, so that I can detect diversification opportunities and correlated risk.

**Acceptance Criteria:**
- [ ] `CorrelationCalculator` computes 60-day rolling Pearson correlation between market index returns
- [ ] Stores daily `CorrelationSnapshot` (matrix as JSON)
- [ ] Detects decorrelation events (correlation deviates > 1σ from 1-year mean)
- [ ] Unit tests with known correlation scenarios

**Technical Notes:**
Uses daily close prices of market benchmarks (SPY, Nifty 50 index). Correlation matrix stored as compressed JSON.

**Dependencies:** STORY-045

---

#### STORY-049: Market Profiles & Config (3 pts)

**Epic:** Market Intelligence | **Priority:** Should Have

**User Story:**
As a system operator, I want market profiles that configure regime thresholds, trading hours, currency, and cost models per market, so that adding a new market requires only config.

**Acceptance Criteria:**
- [ ] `MarketProfile` entity with JSON config (trading hours, currency, regime thresholds, VIX symbol, data source)
- [ ] Default profiles seeded for US_SP500 and IN_NIFTY50
- [ ] Regime classifier reads thresholds from MarketProfile
- [ ] CRUD handler for MarketProfile

**Technical Notes:**
Addresses NFR-006 (extensibility). Adding UK_FTSE100 = create MarketProfile + create StockUniverse + seed candles.

**Dependencies:** STORY-045

---

#### STORY-050: Intelligence API Endpoints (3 pts)

**Epic:** Market Intelligence | **Priority:** Must Have

**User Story:**
As a frontend developer, I want API endpoints for regime, breadth, correlation, and market profile data, so that the dashboard can display market intelligence.

**Acceptance Criteria:**
- [ ] `GET /api/intelligence/regime/{marketCode}` — current regime
- [ ] `GET /api/intelligence/regime/{marketCode}/history` — paginated regime history
- [ ] `GET /api/intelligence/breadth/{marketCode}` — latest breadth snapshot
- [ ] `GET /api/intelligence/correlations` — current correlation matrix
- [ ] `GET /api/intelligence/market-profile/{marketCode}` — market DNA profile
- [ ] All endpoints require authorization, follow IEndpoint pattern

**Technical Notes:**
`IntelligenceEndpoints.cs` following existing auto-discovery pattern. Query handlers read from IntelligenceDbContext.

**Dependencies:** STORY-045, STORY-047

---

### EPIC-004: Risk & Portfolio Intelligence

#### STORY-051: Kelly-Based Position Sizing (5 pts)

**Epic:** Risk & Portfolio | **Priority:** Must Have

**User Story:**
As a trader, I want position sizes calculated using the Kelly Criterion, so that I allocate optimally based on my actual win rate and payoff ratio.

**Acceptance Criteria:**
- [ ] Kelly fraction: `f* = (W × R - L) / R` from rolling 50-trade window
- [ ] Default half-Kelly (f*/2) for safety
- [ ] Position size = `min(Kelly size, existing risk-per-trade, remaining portfolio heat)`
- [ ] Falls back to fixed 1% risk when < 30 closed trades
- [ ] Kelly values update after each closed trade
- [ ] Backtestable: existing engine can replay Kelly sizing historically
- [ ] Unit tests with known win rates and payoff ratios

**Technical Notes:**
Static method in `RiskManager`. Reads trade history from TradingDbContext. Kelly fraction stored with strategy rolling metrics.

**Dependencies:** None (enhances existing PositionSizingConfig)

---

#### STORY-052: Volatility-Targeted Sizing (3 pts)

**Epic:** Risk & Portfolio | **Priority:** Must Have

**User Story:**
As a trader, I want position sizes scaled inversely with volatility (ATR), so that I take bigger positions in calm markets and smaller ones in volatile markets.

**Acceptance Criteria:**
- [ ] Position size = `target risk ($) / (ATR × multiplier)`
- [ ] Target risk derived from Kelly or fixed risk-per-trade
- [ ] ATR period configurable (default: 14 days)
- [ ] Integrates with Kelly sizing: Kelly determines risk budget, vol-targeting determines share count
- [ ] Backtestable in existing engine
- [ ] Unit tests with various ATR levels

**Technical Notes:**
Extends `RiskManager`. Uses existing ATR indicator values from `CandleWithIndicators`.

**Dependencies:** STORY-051

---

#### STORY-053: Correlation-Aware Allocation (5 pts)

**Epic:** Risk & Portfolio | **Priority:** Should Have

**User Story:**
As a trader, I want the system to block or reduce positions that are highly correlated with my existing portfolio, so that I avoid concentrated risk.

**Acceptance Criteria:**
- [ ] Pre-entry: compute 60-day rolling correlation between candidate and all open positions
- [ ] Block entry if avg pairwise correlation > 0.7
- [ ] Reduce position size proportionally when correlation 0.5–0.7
- [ ] Manual override available
- [ ] Correlation check logged with each trade decision
- [ ] Unit tests for block, reduce, and pass scenarios

**Technical Notes:**
Uses `CorrelationCalculator` from STORY-048. Reads open positions from TradingDbContext.

**Dependencies:** STORY-048

---

#### STORY-054: Circuit Breaker (5 pts)

**Epic:** Risk & Portfolio | **Priority:** Must Have

**User Story:**
As a trader, I want the system to automatically halt new trades when my portfolio drawdown exceeds a threshold, so that losses don't compound in adverse conditions.

**Acceptance Criteria:**
- [ ] Triggers when portfolio equity drops X% from peak (default: 15%)
- [ ] Immediately cancels all pending orders
- [ ] Existing positions NOT force-closed (stops still active)
- [ ] Resume requires: regime re-assessment + user approval
- [ ] Configurable threshold per account
- [ ] Circuit breaker events logged with full context (drawdown %, peak equity, current equity, regime)
- [ ] Unit tests for trigger, cancel, and resume flows

**Technical Notes:**
`CircuitBreakerState` tracked in IntelligenceDbContext. Checked at top of daily pipeline before order generation.

**Dependencies:** STORY-045

---

#### STORY-055: Geographic Risk Budget (3 pts)

**Epic:** Risk & Portfolio | **Priority:** Should Have

**User Story:**
As a trader, I want a maximum allocation limit per country/region, so that I don't over-concentrate in a single market.

**Acceptance Criteria:**
- [ ] Configurable max allocation per market (default: 50%)
- [ ] Checked before each new position entry
- [ ] Allocation computed on notional value (shares × current price)
- [ ] Block entry if adding position would exceed budget
- [ ] Unit tests for budget check

**Technical Notes:**
Part of `RiskManager.Evaluate()`. Reads positions from TradingDbContext, groups by market.

**Dependencies:** None

---

### EPIC-007: Execution & Operations

#### STORY-056: Market-Specific Cost Profiles (3 pts)

**Epic:** Execution & Ops | **Priority:** Must Have

**User Story:**
As a trader, I want accurate transaction costs per market applied to backtests and paper trades, so that performance results are realistic.

**Acceptance Criteria:**
- [ ] `CostProfile` entity with: commissions, exchange fees, tax, spread estimate
- [ ] Default profiles: US ($0.005/share + 0.1% spread), India (0.03% brokerage + 0.025% STT + 0.05% spread)
- [ ] Integrated into backtest engine (deducted from each simulated trade)
- [ ] Integrated into paper trading (deducted from paper account)
- [ ] Backtest results show gross vs net returns
- [ ] Unit tests for US and India cost calculations

**Technical Notes:**
`CostProfile` in IntelligenceDbContext. `BacktestEngine` reads cost profile by market code.

**Dependencies:** STORY-045

---

#### STORY-057: Data Quality Monitor (5 pts)

**Epic:** Execution & Ops | **Priority:** Must Have

**User Story:**
As a system operator, I want automatic detection of data quality issues (missing candles, price anomalies, stale data), so that strategies never trade on bad data.

**Acceptance Criteria:**
- [ ] Checks run daily after data ingestion
- [ ] Detects: missing trading days, price gaps > 20%, zero-volume days, stale feeds (no update in 2+ trading days)
- [ ] Affected symbols flagged — strategies skip flagged symbols
- [ ] Alerts logged in `PipelineRunLog`
- [ ] Historical data quality report available per symbol
- [ ] Unit tests for each anomaly type

**Technical Notes:**
Static `DataQualityChecker` class. Runs as step 1 of daily pipeline (STORY-058).

**Dependencies:** STORY-045

---

#### STORY-058: Daily Pipeline Orchestrator (8 pts)

**Epic:** Execution & Ops | **Priority:** Must Have

**User Story:**
As a trader, I want an automated end-of-day pipeline that runs data ingestion → breadth → regime → screen → grade → risk check → orders in sequence, so that the system operates autonomously.

**Acceptance Criteria:**
- [ ] `DailyPipelineService : BackgroundService` orchestrates 10 sequential steps
- [ ] Steps: (1) Data ingest, (2) Data quality check, (3) Breadth computation, (4) Regime classification, (5) Strategy selection, (6) Indicator computation, (7) Screener run, (8) ML scoring (stub — real in Phase 3), (9) Risk checks, (10) Order generation
- [ ] Each step tracked in `PipelineRunLog` with: step name, status, duration, error
- [ ] Failed steps retry 3× with exponential backoff (1s, 5s, 15s)
- [ ] Configurable trigger time per market (US: 21:00 UTC, India: 11:00 UTC)
- [ ] Circuit breaker check before step 10 (skip orders if breaker tripped)
- [ ] `GET /api/feedback/pipeline-status` returns latest run with all step statuses
- [ ] Integration test with seeded data running full pipeline

**Technical Notes:**
Replaces/extends existing `ScreenerSchedulingService`. Each step calls existing handlers via `IMessageBus`. ML scoring step is a no-op stub until STORY-074.

**Dependencies:** STORY-046, STORY-047, STORY-054, STORY-056, STORY-057

---

#### STORY-059: Gradual Capital Deployment (3 pts)

**Epic:** Execution & Ops | **Priority:** Must Have

**User Story:**
As a trader, I want newly promoted strategies to start with reduced capital and scale up as they prove themselves, so that unproven strategies can't blow up the portfolio.

**Acceptance Criteria:**
- [ ] Promoted strategy starts at 25% of target allocation
- [ ] Scales to 50% after 10 profitable trades or 2 weeks (whichever later)
- [ ] Scales to 100% after 25 profitable trades or 4 weeks
- [ ] Immediate scale-down to 25% if drawdown exceeds 5% during ramp
- [ ] Allocation % stored in `TournamentEntry.AllocationPercent`
- [ ] Unit tests for ramp-up and drawdown-triggered scale-down

**Technical Notes:**
Logic in `TournamentManagerService`. AllocationPercent applied as multiplier to RiskManager output.

**Dependencies:** STORY-045

---

### EPIC-002: Claude Strategy Engine

#### STORY-060: Claude API Integration & Prompt Templates (5 pts)

**Epic:** Claude Strategy Engine | **Priority:** Must Have

**User Story:**
As a developer, I want a clean Claude API integration layer with structured prompt templates, so that all AI analysis tasks use consistent, testable prompts.

**Acceptance Criteria:**
- [ ] `Anthropic.SDK` NuGet package added
- [ ] `ClaudeClientWrapper` handles API calls with retry, rate limiting (max 50 calls/day), and error handling
- [ ] Prompt templates for: strategy generation, trade review, autopsy, rule discovery, market profile
- [ ] Each template takes typed input, produces typed output (JSON parsing)
- [ ] API key from environment variable `ANTHROPIC_API_KEY` or `appsettings.json`
- [ ] `FakeClaudeClient` for unit testing (returns canned responses)
- [ ] Rate limiter: track daily call count, refuse calls after limit

**Technical Notes:**
`Infrastructure/Claude/ClaudeClientWrapper.cs`. Templates as static classes with `BuildPrompt()` and `ParseResponse()` methods. All Claude calls async.

**Dependencies:** None

---

#### STORY-061: Strategy Generator (8 pts)

**Epic:** Claude Strategy Engine | **Priority:** Must Have

**User Story:**
As a trader, I want to describe a strategy in natural language and have Claude generate a valid StrategyDefinition, so that I can rapidly prototype trading ideas.

**Acceptance Criteria:**
- [ ] Accepts natural language description (e.g., "momentum strategy for large-cap US stocks in bull markets")
- [ ] Claude generates valid `StrategyDefinition` JSON (compatible with existing v2 engine)
- [ ] Claude explains reasoning for each rule choice
- [ ] Generated strategy auto-backtested on 2 years of data
- [ ] Validation gate: reject if walk-forward Sharpe < 0.5 (returns explanation)
- [ ] `GenerateStrategyCommand` → handler → Claude call → backtest → save or reject
- [ ] `POST /api/feedback/generate-strategy` endpoint
- [ ] Integration test with mocked Claude returning valid StrategyDefinition

**Technical Notes:**
Handler: `GenerateStrategyHandler`. Calls `ClaudeClientWrapper` with strategy generation template. Then calls existing `BacktestEngine` + `WalkForwardAnalyzer` for validation. Two-step: generate → validate.

**Dependencies:** STORY-060, existing backtest engine

---

#### STORY-062: Regime-Based Strategy Selection (5 pts)

**Epic:** Claude Strategy Engine | **Priority:** Must Have

**User Story:**
As a trader, I want the system to automatically select the best strategy for the current market regime, so that I'm always using the approach most likely to work.

**Acceptance Criteria:**
- [ ] Each strategy has a regime-performance matrix (Sharpe per regime from walk-forward)
- [ ] `RegimeChanged` event handler selects best strategy per market for new regime
- [ ] Gradual switchover: new strategy starts at 50% allocation, scales to 100% over 5 days
- [ ] Manual override: user can lock a strategy regardless of regime
- [ ] `StrategyAssignment` entity tracks current assignment per market
- [ ] Selection logic backtestable
- [ ] Unit tests for selection, switchover, and override

**Technical Notes:**
`SelectStrategiesHandler` listens for `RegimeChanged` event (cascading from STORY-047). Reads strategy performance from BacktestDbContext, writes assignment to IntelligenceDbContext.

**Dependencies:** STORY-047, STORY-045

---

#### STORY-063: Market-Specific Playbooks (5 pts)

**Epic:** Claude Strategy Engine | **Priority:** Should Have

**User Story:**
As a trader, I want pre-built strategy templates optimized for each market, so that I have strong starting points instead of building from scratch.

**Acceptance Criteria:**
- [ ] 3 strategy templates per market at launch: momentum, mean-reversion, breakout
- [ ] Templates include market-specific parameters (India: wider stops, higher volume thresholds)
- [ ] Templates tagged with optimal regime(s)
- [ ] Claude generates initial templates based on MarketProfile analysis
- [ ] Templates stored as v2 Strategies with `IsTemplate` flag
- [ ] `GET /api/strategies/templates/{marketCode}` endpoint

**Technical Notes:**
Could be seeded via Claude (STORY-060) or hardcoded initially. Templates are just Strategies with `IsTemplate=true`.

**Dependencies:** STORY-060, STORY-049

---

#### STORY-064: Strategy Autopsy (5 pts)

**Epic:** Claude Strategy Engine | **Priority:** Must Have

**User Story:**
As a trader, I want Claude to write a detailed post-mortem after every losing month, so that I understand what went wrong and how to fix it.

**Acceptance Criteria:**
- [ ] Auto-triggers when any strategy's monthly return < 0
- [ ] Claude analyzes: regime during month, indicator behavior, trade quality, drawdown profile
- [ ] Classifies primary loss reason: regime mismatch, signal degradation, black swan, position sizing error, stop-loss failure
- [ ] Produces actionable recommendations (e.g., "tighten stops", "reduce allocation", "retire")
- [ ] Autopsy report stored in IntelligenceDbContext (linked to strategy + month)
- [ ] `RunAutopsyCommand` handler
- [ ] Unit test with mocked Claude

**Technical Notes:**
Triggered monthly (1st of month) by pipeline or manually. Reads trade history from TradingDbContext, regime from IntelligenceDbContext.

**Dependencies:** STORY-060, STORY-047

---

### EPIC-005: Strategy Tournament

#### STORY-065: Rule Discovery from Trade History (5 pts)

**Epic:** Claude Strategy Engine | **Priority:** Must Have

**User Story:**
As a trader, I want Claude to analyze my trade history and identify what patterns distinguish winners from losers, so that strategy rules can be empirically refined.

**Acceptance Criteria:**
- [ ] Requires minimum 50 closed trades to produce insights
- [ ] Compares indicator snapshots at entry for winners vs losers
- [ ] Claude identifies top 5 distinguishing factors
- [ ] Generates recommended StrategyDefinition modifications
- [ ] Recommendations presented to user for approval before applying
- [ ] `DiscoverRulesCommand` handler
- [ ] Unit test with mocked trade data and Claude

**Technical Notes:**
Needs indicator snapshots (FR-024 from Phase 3). For Phase 2, can use backtested trade logs (already have indicator data in backtest). Full real-trade support comes in Phase 3.

**Dependencies:** STORY-060

---

#### STORY-066: Paper Trading Arena (5 pts)

**Epic:** Strategy Tournament | **Priority:** Must Have

**User Story:**
As a trader, I want to run 10-20 strategy candidates simultaneously in paper trading, so that I can see which ones actually work before committing real capital.

**Acceptance Criteria:**
- [ ] `TournamentRun` entity tracks tournament cycle metadata (start date, market, status)
- [ ] `TournamentEntry` entity per strategy in tournament
- [ ] Each entry gets an isolated paper Account (uses existing paper trading infra)
- [ ] `EnterTournamentCommand` creates paper account + entry
- [ ] Daily: pipeline generates signals per tournament strategy, places paper orders
- [ ] 10-20 concurrent entries per market supported
- [ ] Unit tests for entry creation and isolation

**Technical Notes:**
`TournamentEntry` in IntelligenceDbContext. Paper `Account` in TradingDbContext. Cross-DB via Guid reference. `TournamentManagerService` drives daily execution.

**Dependencies:** STORY-045, existing paper trading (STORY-012/013)

---

#### STORY-067: Tournament Leaderboard & Tracking (5 pts)

**Epic:** Strategy Tournament | **Priority:** Must Have

**User Story:**
As a trader, I want a live tournament leaderboard showing each strategy's P&L, Sharpe, drawdown, and win rate, so that I can see which strategies are performing best.

**Acceptance Criteria:**
- [ ] Daily metrics update per entry: total return, Sharpe, max drawdown, win rate, trade count
- [ ] 30-day minimum evaluation period before promotion eligibility
- [ ] `GET /api/tournament/leaderboard/{marketCode}` — sorted by Sharpe
- [ ] `GET /api/tournament/entries/{entryId}` — detailed entry with daily equity curve
- [ ] `GET /api/tournament/active-strategies/{marketCode}` — currently promoted strategies
- [ ] Unit tests for metric computation

**Technical Notes:**
Metrics computed in `TournamentManagerService` daily cycle. Reads paper Account portfolio from TradingDbContext.

**Dependencies:** STORY-066

---

#### STORY-068: Strategy Promotion & Retirement (5 pts)

**Epic:** Strategy Tournament | **Priority:** Must Have

**User Story:**
As a trader, I want top-performing strategies automatically promoted and underperformers retired, so that only proven strategies get capital.

**Acceptance Criteria:**
- [ ] Promotion criteria: 30+ days, Sharpe > 1.0, max DD < 10%, win rate > 50%
- [ ] Promoted strategies start at 25% allocation (STORY-059 ramp)
- [ ] Retirement criteria: Sharpe < 0.3 over rolling 60 days, or 3 consecutive losing months
- [ ] Retired strategies archived with full performance history
- [ ] User notified of all promotions and retirements with reasoning
- [ ] Manual override: `POST /api/tournament/promote/{entryId}`, `POST /api/tournament/retire/{entryId}`
- [ ] Unit tests for promotion, retirement, and edge cases

**Technical Notes:**
`PromoteStrategyHandler` + `RetireStrategyHandler`. Promotion returns `StrategyPromoted` event; retirement returns `StrategyRetired` event.

**Dependencies:** STORY-066, STORY-067, STORY-059

---

#### STORY-069: Ensemble Signal Voting (3 pts)

**Epic:** Strategy Tournament | **Priority:** Should Have

**User Story:**
As a trader, I want signals from multiple active strategies aggregated via voting, so that I only trade when there's consensus.

**Acceptance Criteria:**
- [ ] Aggregates signals across all active strategies per symbol per day
- [ ] Requires configurable minimum agreement (default: 2 of 3)
- [ ] Optional Sharpe-weighted voting (higher Sharpe = more weight)
- [ ] Ensemble signal logged with individual strategy votes
- [ ] Backtestable: can replay ensemble logic on historical signals
- [ ] Unit tests for majority, weighted, and tie scenarios

**Technical Notes:**
Called after screener runs in daily pipeline. Filters pipeline output to only consensus signals.

**Dependencies:** STORY-066

---

#### STORY-070: Tournament & Claude API Endpoints (3 pts)

**Epic:** Strategy Tournament | **Priority:** Must Have

**User Story:**
As a frontend developer, I want API endpoints for tournament management, so that the UI can show leaderboards and trigger promotions.

**Acceptance Criteria:**
- [ ] `POST /api/tournament/enter` — enter strategy into tournament
- [ ] `POST /api/tournament/promote/{entryId}` — manual promote
- [ ] `POST /api/tournament/retire/{entryId}` — manual retire
- [ ] All endpoints require authorization, follow IEndpoint pattern
- [ ] `TournamentEndpoints.cs` auto-discovered

**Technical Notes:**
Thin endpoint layer calling handlers via `IMessageBus`.

**Dependencies:** STORY-066

---

### EPIC-003: ML Confidence Pipeline

#### STORY-071: Feature Store & Indicator Snapshot (5 pts)

**Epic:** ML Pipeline | **Priority:** Must Have

**User Story:**
As a system, I want to capture the full indicator state at every trade entry, so that ML models can learn which conditions predict profitable trades.

**Acceptance Criteria:**
- [ ] `FeatureSnapshot` entity stores 40+ features per trade at entry
- [ ] Features: all indicator values, regime label, sector, market, day-of-week, days since regime change, win/loss streak, portfolio heat, relative volume, Bollinger %B
- [ ] Linked to trade via `TradeId` (Guid reference to TradingDb.Order)
- [ ] Feature schema versioned (`FeatureVersion` field)
- [ ] Stored as compressed JSON with SHA256 hash for integrity
- [ ] `CaptureFeatureSnapshotHandler` triggers on trade entry
- [ ] Outcome (`TradeOutcome`, `TradePnlPercent`) updated on trade close
- [ ] Unit tests for feature extraction completeness

**Technical Notes:**
`FeatureSnapshot` in IntelligenceDbContext. Handler cascades from order placement event.

**Dependencies:** STORY-045, STORY-047

---

#### STORY-072: Feature Extractor (5 pts)

**Epic:** ML Pipeline | **Priority:** Must Have

**User Story:**
As a developer, I want a typed feature extraction pipeline that converts indicator state into ML-ready feature vectors, so that model training is consistent and reproducible.

**Acceptance Criteria:**
- [ ] `FeatureExtractor.Extract()` takes `CandleWithIndicators` + context → `FeatureVector`
- [ ] `FeatureVector` is a C# class with typed properties (ML.NET compatible)
- [ ] Batch extraction: load N FeatureSnapshots → N FeatureVectors for training
- [ ] Feature names match between extraction and ML schema
- [ ] Handles missing indicator values gracefully (warmup period → default values)
- [ ] Unit tests verifying feature count and value correctness

**Technical Notes:**
`FeatureVector` in `Infrastructure/ML/`. Properties decorated with `[ColumnName]` attributes for ML.NET.

**Dependencies:** STORY-071

---

#### STORY-073: ML Model Training Pipeline (8 pts)

**Epic:** ML Pipeline | **Priority:** Must Have

**User Story:**
As a trader, I want an ML model trained on my trade history to predict which signals are likely to be profitable, so that signal quality improves over time.

**Acceptance Criteria:**
- [ ] ML.NET + LightGBM setup (`Microsoft.ML`, `Microsoft.ML.LightGbm` NuGet packages)
- [ ] `MlModelTrainer.Train()` takes training + validation data → trained model
- [ ] Target: binary classification (profitable=1, losing=0)
- [ ] Walk-forward training: train on first N trades, validate on next M (no future leakage)
- [ ] Model outputs probability score 0.0–1.0
- [ ] Feature importance report (top 10 features) via `PermutationFeatureImportance`
- [ ] Model saved as `.zip` in `data/models/{marketCode}/v{version}.zip`
- [ ] `MlModel` entity stores metadata: AUC, precision, recall, F1, training samples, feature version
- [ ] Minimum AUC > 0.55 required for model to be active (otherwise no-op)
- [ ] `RetrainModelCommand` handler
- [ ] Unit test with synthetic training data

**Technical Notes:**
Training runs in `MlRetrainService` background service. Model file stored on filesystem, metadata in IntelligenceDbContext. Hyperparams: numLeaves=31, learningRate=0.1, numIterations=100 (defaults).

**Dependencies:** STORY-072, STORY-071

---

#### STORY-074: ML Prediction Service & Confidence Integration (5 pts)

**Epic:** ML Pipeline | **Priority:** Must Have

**User Story:**
As a trader, I want ML confidence scores integrated into the existing A-F grading system, so that learned patterns improve signal quality.

**Acceptance Criteria:**
- [ ] `MlPredictionService` singleton loads active model per market at startup
- [ ] `PredictConfidence(marketCode, features)` returns probability 0.0–1.0
- [ ] Inference < 10ms per prediction (benchmark test)
- [ ] ML Confidence added as 7th factor in `ConfidenceGrader` (15% weight)
- [ ] Existing 6 factors re-weighted proportionally (sum = 100%)
- [ ] When no model available (< 200 trades), original 6-factor grading unchanged
- [ ] A/B comparison: grade with and without ML factor
- [ ] Daily pipeline step 8 now calls ML scoring (replaces stub from STORY-058)
- [ ] ML endpoints: `GET /api/ml/models/{marketCode}/active`, `GET /api/ml/feature-importance/{marketCode}`

**Technical Notes:**
`PredictionEngine<FeatureVector, TradePrediction>` is thread-safe singleton. Reloaded on model retrain event. `ConfidenceGrader.Grade()` extended with optional `mlConfidence` parameter.

**Dependencies:** STORY-073, STORY-058

---

### EPIC-006: Feedback & Learning System

#### STORY-075: Model Retraining Pipeline (5 pts)

**Epic:** ML Pipeline | **Priority:** Must Have

**User Story:**
As a trader, I want the ML model automatically retrained on a schedule, so that it stays current with evolving market patterns.

**Acceptance Criteria:**
- [ ] `MlRetrainService : BackgroundService` triggers retraining monthly or every 50 new closed trades
- [ ] Walk-forward: always train on historical, validate on most recent window
- [ ] New model version stored alongside previous
- [ ] Auto-rollback: if new model AUC < previous model AUC, keep old model active
- [ ] Retraining logs and metrics stored in `MlModel` entity
- [ ] `POST /api/ml/retrain/{marketCode}` for manual trigger
- [ ] `ModelTrained` event published on successful training
- [ ] Feature drift detection: alert when feature distributions shift significantly
- [ ] Unit test for rollback scenario

**Technical Notes:**
Background service checks every hour if retraining criteria met. Uses `MlModelTrainer` from STORY-073.

**Dependencies:** STORY-073

---

#### STORY-076: AI Trade Journal (5 pts)

**Epic:** Feedback & Learning | **Priority:** Must Have

**User Story:**
As a trader, I want every closed trade automatically reviewed by Claude with qualitative analysis, so that I build institutional knowledge about what works and what doesn't.

**Acceptance Criteria:**
- [ ] Auto-journal captures: symbol, entry/exit price, duration, P&L, regime at entry/exit, strategy, grade, ML confidence, all indicator values
- [ ] Claude review within 24 hours of trade close
- [ ] Review classifies: outcome class (GoodEntryGoodExit, GoodEntryBadExit, BadEntry, RegimeMismatch, StoppedCorrectly, StoppedPrematurely)
- [ ] Review classifies: mistake type for losers (BadSignal, BadTiming, RegimeMismatch, StopTooTight, StopTooLoose, OversizedPosition, CorrelatedLoss, BlackSwan)
- [ ] `TradeReview` entity stored in IntelligenceDbContext
- [ ] `ReviewTradeCommand` handler (enqueued by trade close event)
- [ ] `GET /api/feedback/trade-reviews` — paginated, filterable
- [ ] `GET /api/feedback/trade-reviews/{tradeId}` — specific review
- [ ] Unit test with mocked Claude

**Technical Notes:**
`ClaudeAnalysisService` processes review queue. Reads FeatureSnapshot for context. Trade review prompt uses structured JSON output.

**Dependencies:** STORY-060, STORY-071

---

#### STORY-077: Strategy Decay Monitor (5 pts)

**Epic:** Feedback & Learning | **Priority:** Must Have

**User Story:**
As a trader, I want the system to detect when a strategy's edge is eroding, so that I can adapt or retire it before significant losses.

**Acceptance Criteria:**
- [ ] Tracks rolling 30/60/90-day Sharpe, win rate, avg trade P&L per strategy
- [ ] Warning alert: rolling 60-day Sharpe drops below 50% of historical average
- [ ] Severe alert: rolling 30-day Sharpe goes negative
- [ ] `StrategyDecayAlert` entity with alert type, metrics, resolved flag
- [ ] Claude analysis of probable cause (enqueued on alert)
- [ ] `GET /api/feedback/decay-alerts` — active alerts
- [ ] Backtestable against historical performance
- [ ] Unit tests for warning and severe thresholds

**Technical Notes:**
`CheckDecayHandler` runs after each trade close. Reads trade history, computes rolling metrics, compares to stored historical baseline.

**Dependencies:** STORY-045, STORY-060

---

#### STORY-078: Mistake Taxonomy & Pattern Extraction (5 pts)

**Epic:** Feedback & Learning | **Priority:** Should Have

**User Story:**
As a trader, I want every losing trade classified into a mistake type and patterns extracted periodically, so that systematic errors are identified and corrected.

**Acceptance Criteria:**
- [ ] 8 taxonomy categories: BadSignal, BadTiming, RegimeMismatch, StopTooTight, StopTooLoose, OversizedPosition, CorrelatedLoss, BlackSwan
- [ ] Classification automated via rule-based heuristics (Claude override for ambiguous)
- [ ] Pattern report generated every 50 closed trades per market
- [ ] Report: most common mistake, which market/regime produces most mistakes, recommendations
- [ ] `GET /api/feedback/mistake-summary/{marketCode}` — breakdown
- [ ] Recommendations feed into rule discovery (STORY-065)
- [ ] Unit tests for each classification heuristic

**Technical Notes:**
Heuristics: StopTooTight = stopped out within 1 ATR, trade later would have been profitable. RegimeMismatch = regime changed during trade. OversizedPosition = loss > 2× average loss.

**Dependencies:** STORY-076

---

#### STORY-079: Monthly Performance Attribution (3 pts)

**Epic:** Feedback & Learning | **Priority:** Should Have

**User Story:**
As a trader, I want monthly returns decomposed into alpha, beta, regime contribution, and residual, so that I know whether profits come from skill or market exposure.

**Acceptance Criteria:**
- [ ] Decomposes: alpha (strategy edge), beta (market exposure), regime (regime positioning), residual (noise)
- [ ] Uses benchmark (SPY for US, Nifty 50 for India) for beta
- [ ] Monthly attribution report auto-generated
- [ ] Rolling 12-month attribution summary
- [ ] `GET /api/feedback/attribution/{marketCode}/{year}/{month}`
- [ ] Unit tests with known alpha/beta scenarios

**Technical Notes:**
`PerformanceAttributor` static class. Beta = covariance(strategy, benchmark) / variance(benchmark). Alpha = strategy return - (beta × benchmark return).

**Dependencies:** STORY-045, existing benchmark data

---

#### STORY-080: ML & Feedback API Endpoints (3 pts)

**Epic:** Feedback & Learning | **Priority:** Must Have

**User Story:**
As a frontend developer, I want API endpoints for ML model info, trade reviews, decay alerts, and attribution, so that the feedback dashboard has data.

**Acceptance Criteria:**
- [ ] `MlEndpoints.cs`: model registry, active model, feature importance, manual retrain
- [ ] `FeedbackEndpoints.cs`: trade reviews, decay alerts, attribution, mistake summary, pipeline status
- [ ] All endpoints require authorization, follow IEndpoint pattern
- [ ] Auto-discovered via existing reflection-based `MapEndpoints()`

**Technical Notes:**
Thin endpoint layer. Query handlers read from IntelligenceDbContext.

**Dependencies:** STORY-074, STORY-076, STORY-077

---

## Sprint Allocation

### Sprint 10 — Market Intelligence Foundation (24 pts)

**Goal:** Stand up the Intelligence bounded context and deliver regime-aware market classification across US and India markets.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-045 | IntelligenceDbContext & base entities | 5 | Must |
| STORY-046 | Breadth Engine | 5 | Must |
| STORY-047 | Market Regime Classifier | 5 | Must |
| STORY-048 | Cross-Market Correlation Monitor | 3 | Should |
| STORY-049 | Market Profiles & Config | 3 | Should |
| STORY-050 | Intelligence API Endpoints | 3 | Must |

**Committed:** 24 / 30 capacity
**Deliverable:** System classifies US and India markets into regimes daily. API exposes regime, breadth, correlation data.

---

### Sprint 11 — Risk & Portfolio Intelligence (24 pts)

**Goal:** Professional-grade risk management with Kelly sizing, volatility targeting, correlation checks, and circuit breaker protection.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-051 | Kelly-Based Position Sizing | 5 | Must |
| STORY-052 | Volatility-Targeted Sizing | 3 | Must |
| STORY-053 | Correlation-Aware Allocation | 5 | Should |
| STORY-054 | Circuit Breaker | 5 | Must |
| STORY-055 | Geographic Risk Budget | 3 | Should |
| STORY-056 | Market-Specific Cost Profiles | 3 | Must |

**Committed:** 24 / 30 capacity
**Deliverable:** Every trade sized by Kelly + vol-targeting. Portfolio protected by circuit breaker. Costs are market-accurate.

---

### Sprint 12 — Operations + Claude Setup (24 pts)

**Goal:** Autonomous daily pipeline operational. Claude API integrated and ready for strategy generation.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-057 | Data Quality Monitor | 5 | Must |
| STORY-058 | Daily Pipeline Orchestrator | 8 | Must |
| STORY-059 | Gradual Capital Deployment | 3 | Must |
| STORY-060 | Claude API Integration & Prompt Templates | 5 | Must |
| STORY-070 | Tournament & Claude API Endpoints | 3 | Must |

**Committed:** 24 / 30 capacity
**Deliverable:** System runs end-to-end autonomously each market close. Claude SDK wired up and tested.

**Phase 1 complete after Sprint 12.** System is regime-aware, risk-managed, and operationally autonomous.

---

### Sprint 13 — Claude Strategy Engine (23 pts)

**Goal:** Claude generates, selects, and diagnoses trading strategies.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-061 | Strategy Generator | 8 | Must |
| STORY-062 | Regime-Based Strategy Selection | 5 | Must |
| STORY-063 | Market-Specific Playbooks | 5 | Should |
| STORY-064 | Strategy Autopsy | 5 | Must |

**Committed:** 23 / 30 capacity
**Deliverable:** Describe a strategy in English → Claude generates it → backtest validates it → regime selector assigns it.

---

### Sprint 14 — Tournament + Rule Discovery (23 pts)

**Goal:** Strategy tournament running with automated promotion and retirement.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-065 | Rule Discovery from Trade History | 5 | Must |
| STORY-066 | Paper Trading Arena | 5 | Must |
| STORY-067 | Tournament Leaderboard & Tracking | 5 | Must |
| STORY-068 | Strategy Promotion & Retirement | 5 | Must |
| STORY-069 | Ensemble Signal Voting | 3 | Should |

**Committed:** 23 / 30 capacity
**Deliverable:** 20 strategies compete in paper. Best get promoted. Worst get retired. Ensemble voting filters signals.

**Phase 2 complete after Sprint 14.** Claude generates strategies, tournament validates them, winners get capital.

---

### Sprint 15 — ML Pipeline Core (23 pts)

**Goal:** ML model training and prediction integrated into confidence grading.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-071 | Feature Store & Indicator Snapshot | 5 | Must |
| STORY-072 | Feature Extractor | 5 | Must |
| STORY-073 | ML Model Training Pipeline | 8 | Must |
| STORY-074 | ML Prediction Service & Confidence Integration | 5 | Must |

**Committed:** 23 / 30 capacity
**Deliverable:** Every trade gets an ML confidence score. Grading system uses 7 factors. Model improves with data.

---

### Sprint 16 — Feedback Loop & ML Ops (26 pts)

**Goal:** Close the learning loop. System reviews trades, detects decay, extracts patterns, attributes performance.

| Story | Title | Pts | Priority |
|-------|-------|-----|----------|
| STORY-075 | Model Retraining Pipeline | 5 | Must |
| STORY-076 | AI Trade Journal | 5 | Must |
| STORY-077 | Strategy Decay Monitor | 5 | Must |
| STORY-078 | Mistake Taxonomy & Pattern Extraction | 5 | Should |
| STORY-079 | Monthly Performance Attribution | 3 | Should |
| STORY-080 | ML & Feedback API Endpoints | 3 | Must |

**Committed:** 26 / 30 capacity
**Deliverable:** Full feedback loop operational — trades reviewed, decay detected, patterns extracted, ML retrained.

**Phase 3 complete after Sprint 16.** The system learns from every trade and improves over time.

---

## Epic Traceability

| Epic | Name | Stories | Points | Sprints |
|------|------|---------|--------|---------|
| EPIC-001 | Market Intelligence Layer | 045–050 | 24 | 10 |
| EPIC-002 | Claude Strategy Engine | 060–065 | 33 | 12–14 |
| EPIC-003 | ML Confidence Pipeline | 071–075 | 28 | 15–16 |
| EPIC-004 | Risk & Portfolio Intelligence | 051–055 | 21 | 11 |
| EPIC-005 | Strategy Tournament | 066–070 | 21 | 12, 14 |
| EPIC-006 | Feedback & Learning | 076–080 | 21 | 16 |
| EPIC-007 | Execution & Operations | 056–059 | 19 | 11–12 |

---

## FR Coverage

| FR | Name | Story | Sprint |
|----|------|-------|--------|
| FR-001 | Regime Classifier | STORY-047 | 10 |
| FR-002 | Market DNA Profiles | STORY-049 | 10 |
| FR-003 | Correlation Monitor | STORY-048 | 10 |
| FR-004 | Breadth Engine | STORY-046 | 10 |
| FR-005 | Macro Overlay | STORY-049 (config) | 10 |
| FR-006 | Strategy Generator | STORY-061 | 13 |
| FR-007 | Rule Discovery | STORY-065 | 14 |
| FR-008 | Regime-Based Selection | STORY-062 | 13 |
| FR-009 | Market Playbooks | STORY-063 | 13 |
| FR-010 | Strategy Autopsy | STORY-064 | 13 |
| FR-011 | Feature Store | STORY-071 | 15 |
| FR-012 | ML Predictor | STORY-073 | 15 |
| FR-013 | ML Confidence Integration | STORY-074 | 15 |
| FR-014 | Model Retraining | STORY-075 | 16 |
| FR-015 | Kelly Sizing | STORY-051 | 11 |
| FR-016 | Correlation Allocation | STORY-053 | 11 |
| FR-017 | Circuit Breaker | STORY-054 | 11 |
| FR-018 | Vol-Target Sizing | STORY-052 | 11 |
| FR-019 | Geographic Budget | STORY-055 | 11 |
| FR-020 | Paper Arena | STORY-066 | 14 |
| FR-021 | Promote/Retire | STORY-068 | 14 |
| FR-022 | Ensemble Voting | STORY-069 | 14 |
| FR-023 | AI Trade Journal | STORY-076 | 16 |
| FR-024 | Indicator Snapshot | STORY-071 | 15 |
| FR-025 | Decay Monitor | STORY-077 | 16 |
| FR-026 | Mistake Taxonomy | STORY-078 | 16 |
| FR-027 | Performance Attribution | STORY-079 | 16 |
| FR-028 | Cost Model | STORY-056 | 11 |
| FR-029 | Data Quality Monitor | STORY-057 | 12 |
| FR-030 | Gradual Deployment | STORY-059 | 12 |

**Coverage: 30/30 FRs**

---

## Risks & Mitigation

**High:**
- **Claude API quality**: Generated strategies may be invalid JSON or nonsensical rules → Mitigation: validation gate (auto-backtest, Sharpe threshold), structured JSON prompts with examples
- **ML model overfitting on small datasets**: < 200 trades may not be enough → Mitigation: AUC threshold (0.55), falls back to rule-only grading, walk-forward validation
- **India market data gaps**: Yahoo Finance has inconsistent NSE data → Mitigation: Data quality monitor (STORY-057), supplement with NSE CSV downloads

**Medium:**
- **Claude API costs**: 50 calls/day at ~$0.05/call = $2.50/day → Mitigation: rate limiter, batch processing, prioritize reviews over generation
- **Background service complexity**: 5 new services + existing 2 → Mitigation: DailyPipelineService orchestrates most, others run independently

**Low:**
- **ML.NET LightGBM version lag**: Slightly behind Python → Mitigation: for 30-50 features on < 10K rows, performance is equivalent

---

## Definition of Done

For a story to be considered complete:
- [ ] Code implemented and committed
- [ ] Unit tests written and passing
- [ ] Integration with existing codebase verified (no regressions)
- [ ] Follows existing patterns (static handlers, IEndpoint, DataCache, etc.)
- [ ] Acceptance criteria validated
- [ ] Builds successfully

---

## Next Steps

**Immediate:** Begin Sprint 10 — STORY-045 (IntelligenceDbContext) is the foundation.

Run `/bmad:dev-story STORY-045` to start implementation.

**Sprint cadence:**
- Sprints 10–12: Phase 1 (Market Intelligence + Risk + Operations)
- Sprints 13–14: Phase 2 (Claude + Tournament)
- Sprints 15–16: Phase 3 (ML + Feedback)

---

*Generated by BMAD Method v6 — Scrum Master*
*Sprint Planning Session: 2026-03-02*
