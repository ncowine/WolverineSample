# Architecture: Adaptive Trading Intelligence Engine

**Version:** 1.0
**Date:** 2026-03-02
**Author:** System Architect (BMAD)
**Status:** Draft
**PRD Reference:** `docs/prd-adaptive-trading-intelligence-2026-03-02.md`

---

## 1. Architectural Drivers

These NFRs and FRs most heavily influence the design:

| Driver | Requirement | Impact on Architecture |
|--------|-------------|----------------------|
| NFR-001 | Backtest < 5s per symbol; walk-forward < 10min | Pure computation in-process; no network hops for backtest path |
| NFR-002 | ML inference < 10ms per prediction | In-process ML (ML.NET), not a Python sidecar |
| NFR-003 | Immutable trade journal + indicator snapshots | Append-only tables, compressed JSON, hash verification |
| NFR-004 | Background services must be resilient | Retry + dead-letter pattern; health check endpoint |
| NFR-006 | New markets via config, not code changes | Market profile entity + config-driven regime thresholds |
| NFR-007 | ML model versioning + auto-rollback | Model registry table; active model per market |
| FR-001 | Regime classifier runs daily per market | Background service, stores regime history |
| FR-008 | Strategy selection changes on regime transition | Event-driven: RegimeChanged → StrategySelector |
| FR-012 | ML model trains on closed trade features | ML.NET LightGBM trainer; walk-forward split |
| FR-020 | 10-20 concurrent paper strategies | Tournament uses existing paper Account infrastructure |

---

## 2. High-Level Architecture

### Pattern: Extended Modular Monolith (Event-Driven)

The existing system is already a modular monolith with 3 bounded contexts and Wolverine event cascading. We extend it with a **4th bounded context: Intelligence** — containing regime detection, strategy orchestration, ML pipeline, tournament management, and feedback analysis.

Claude API integration is **asynchronous batch** — a background service that runs post-market, never in the real-time request path.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           ANGULAR CLIENT                                │
│                  Dashboard · Tournament · Regime · Journal              │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ HTTP/REST
┌───────────────────────────────▼─────────────────────────────────────────┐
│                         API LAYER (Endpoints)                           │
│   Existing 26 endpoints + new Intelligence endpoints (~12 new)         │
│   JWT Auth · Validation Middleware · IEndpoint auto-discovery           │
└───────┬──────────┬──────────┬──────────┬────────────────────────────────┘
        │          │          │          │
        ▼          ▼          ▼          ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────────────────┐
│ MarketData│ │ Trading  │ │Backtesting│ │      INTELLIGENCE (NEW)     │
│  Context  │ │ Context  │ │ Context   │ │                             │
│           │ │          │ │           │ │  Regime · Tournament · ML   │
│ Stocks    │ │ Accounts │ │Strategies │ │  Feedback · Claude Bridge   │
│ Candles   │ │ Orders   │ │Backtests  │ │                             │
│ Indicators│ │ Positions│ │Results    │ │  MarketRegime  · MLModel    │
│ Screener  │ │ Portfolio│ │WalkFwd    │ │  TournamentRun · TradeReview│
│ Universe  │ │ DCA Plans│ │OptParams  │ │  FeatureSnapshot · Decay    │
│ Breadth   │ │          │ │           │ │  StrategyAssignment         │
│ (NEW)     │ │          │ │           │ │                             │
└─────┬─────┘ └────┬─────┘ └─────┬────┘ └──────────────┬──────────────┘
      │            │             │                      │
      ▼            ▼             ▼                      ▼
   SQLite       SQLite        SQLite                 SQLite
  market.db    trading.db   backtest.db          intelligence.db
                                                     (NEW)

┌─────────────────────────────────────────────────────────────────────────┐
│                      BACKGROUND SERVICES (5 new)                        │
│                                                                         │
│  ┌─────────────┐ ┌───────────────┐ ┌──────────────┐ ┌──────────────┐  │
│  │  DailyPipe-  │ │  Tournament   │ │  ML Retrain  │ │  Claude      │  │
│  │  lineService │ │  Manager      │ │  Service     │ │  Analysis    │  │
│  │              │ │  Service      │ │              │ │  Service     │  │
│  │ • Ingest data│ │ • Run paper   │ │ • Train model│ │ • Strategy   │  │
│  │ • Breadth    │ │   strategies  │ │ • Validate   │ │   generation │  │
│  │ • Regime     │ │ • Track P&L   │ │ • Rollback   │ │ • Trade      │  │
│  │ • Screen     │ │ • Promote/    │ │ • Feature    │ │   review     │  │
│  │ • ML score   │ │   retire      │ │   drift check│ │ • Autopsy    │  │
│  │ • Grade      │ │               │ │              │ │ • Rule disc. │  │
│  └──────────────┘ └───────────────┘ └──────────────┘ └──────────────┘  │
│                                                                         │
│  ┌──────────────┐                                                       │
│  │  Existing:   │                                                       │
│  │  DCA Service │                                                       │
│  │  Screener    │                                                       │
│  │  Scheduling  │                                                       │
│  └──────────────┘                                                       │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                      WOLVERINE MESSAGE BUS                              │
│                                                                         │
│  Commands:  ClassifyRegime, RunTournamentCycle, RetrainModel,          │
│             GenerateStrategy, ReviewTrade, RunAutopsy                   │
│                                                                         │
│  Events:    RegimeChanged, StrategyPromoted, StrategyRetired,          │
│             ModelTrained, TradeReviewed, DecayDetected                  │
│                                                                         │
│  Cascading: RegimeChanged → SelectStrategies → StrategyAssigned        │
│             TradeClosedn → SnapshotFeatures → UpdateFeatureStore       │
│             ModelTrained → ScoreBacklog → MLScoresUpdated              │
└─────────────────────────────────────────────────────────────────────────┘
```

### Why Not a Separate Microservice for Intelligence?

| Consideration | Decision |
|---------------|----------|
| Backtest engine needs indicator data in-process | Cross-service calls would kill NFR-001 (< 5s backtest) |
| ML inference must be < 10ms | Network hop to ML service adds latency; ML.NET runs in-process |
| Single developer/team | Microservice overhead not justified |
| SQLite databases | Already local; no benefit from splitting processes |
| Wolverine event cascading | Works within a single process; cross-process needs Rabbit/Kafka |

**Decision: Stay monolith.** Add a 4th bounded context (IntelligenceDbContext) within the same process. This preserves all existing patterns and allows in-process access to indicators, backtest engine, and ML scoring.

---

## 3. Technology Stack

### Existing (Unchanged)
| Layer | Technology | Rationale |
|-------|-----------|-----------|
| Backend | .NET 8 + Wolverine 3.x | Already in place; CQRS + event cascading |
| Frontend | Angular 21 + Tailwind/DaisyUI | Already in place |
| Database | SQLite (3 contexts) | Already in place |
| Auth | JWT Bearer | Already in place |
| Validation | FluentValidation | Already in place |
| Caching | DataCache<TKey,TValue> | Already in place |

### New Additions

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **ML Runtime** | **ML.NET 4.x + LightGBM trainer** | Native .NET; no Python sidecar needed; LightGBM via `Microsoft.ML.LightGbm` NuGet; fast inference (< 1ms per prediction); feature importance built-in |
| **Claude Integration** | **Anthropic .NET SDK** (`Anthropic.SDK` NuGet) | Official SDK; async/streaming support; used in background services only |
| **ML Model Storage** | **File system** (`data/models/`) + metadata in SQLite | ONNX-compatible export; version folders; metadata in IntelligenceDbContext |
| **4th Database** | **SQLite** (`intelligence.db`) | Consistent with existing pattern; regime, tournament, ML, feedback data |

### ML.NET Architecture Decision

**Why ML.NET over Python sidecar:**
- Zero deployment complexity (single .NET process)
- < 1ms inference latency (in-process, no serialization/IPC)
- LightGBM trainer available via NuGet (`Microsoft.ML.LightGbm`)
- Feature importance via `PermutationFeatureImportance` API
- Walk-forward cross-validation via `mlContext.Data.TrainTestSplit()`
- Model saved as `.zip` file, loaded via `mlContext.Model.Load()`

**Trade-off:** ML.NET's LightGBM is slightly behind Python's LightGBM in version, but for tabular classification with 30-50 features on < 10K rows, performance difference is negligible.

### Claude API Integration Decision

**Why async batch, not real-time:**
- Claude API latency is 2-10 seconds per call — unacceptable in daily pipeline critical path
- Strategy generation is a creative task, not time-sensitive (run overnight)
- Trade review can happen 24h after trade close without impact
- Cost: batch calls are cheaper than many small real-time calls
- Rate limits: batching avoids throttling

**Pattern:** `ClaudeAnalysisService` (BackgroundService) processes a queue of analysis requests. Each request type (StrategyGeneration, TradeReview, Autopsy, RuleDiscovery) has a Wolverine command that enqueues work.

---

## 4. New Bounded Context: Intelligence

### 4.1 IntelligenceDbContext

A 4th DbContext following the same patterns as the existing three.

```
IntelligenceDbContext
├── MarketRegime           — Current + historical regime per market
├── RegimeTransition       — Timestamped regime change events
├── MarketProfile          — Claude-generated market DNA profiles
├── CorrelationSnapshot    — Daily cross-market correlation matrix
├── BreadthSnapshot        — Daily breadth indicators per market
├── TournamentRun          — Tournament cycle metadata
├── TournamentEntry        — Strategy performance in tournament
├── StrategyAssignment     — Which strategy is active per market + regime
├── FeatureSnapshot        — Indicator state at trade entry (ML features)
├── MlModel                — Model registry (version, metrics, active flag)
├── MlPrediction           — Predictions linked to trades (for validation)
├── TradeReview            — Claude's AI review of each trade
├── StrategyDecayAlert     — Decay detection alerts
├── PerformanceAttribution — Monthly return decomposition
└── CostProfile            — Market-specific transaction cost configs
```

**Connection String:** `Data Source=data/intelligence.db`

**Registered in Program.cs:**
```csharp
builder.Services.AddDbContext<IntelligenceDbContext>(options =>
    options.UseSqlite("Data Source=data/intelligence.db"));
```

### 4.2 Domain Entities (New)

#### MarketRegime
```
MarketRegime : BaseEntity
├── MarketCode        : string         — "US_SP500", "IN_NIFTY50", "UK_FTSE100"
├── CurrentRegime     : RegimeType     — Bull, Bear, Sideways, HighVolatility
├── RegimeStartDate   : DateTime       — When current regime began
├── RegimeDuration    : int            — Days in current regime
├── SmaSlope50        : decimal        — 50-day SMA slope (positive = bullish)
├── SmaSlope200       : decimal        — 200-day SMA slope
├── VixLevel          : decimal        — VIX (US) or India VIX
├── BreadthScore      : decimal        — Composite breadth (0-100)
├── PctAbove200Sma    : decimal        — % of universe above 200 SMA
├── AdvanceDeclineRatio: decimal       — A/D ratio
├── ClassifiedAt      : DateTime       — When last classified
└── ConfidenceScore   : decimal        — Classifier confidence (0-1)
```

#### FeatureSnapshot
```
FeatureSnapshot : BaseEntity
├── TradeId           : Guid           — FK to Order/Trade
├── Symbol            : string
├── MarketCode        : string
├── SnapshotDate      : DateTime
├── RegimeAtEntry     : RegimeType
├── FeaturesJson      : string         — Compressed JSON of all 40+ features
├── FeatureVersion    : int            — Schema version for forward compat
├── TradeOutcome      : TradeOutcome?  — Win, Loss, Breakeven (set on close)
├── TradePnlPercent   : decimal?       — % return (set on close)
└── MlPrediction      : decimal?       — ML confidence at time of entry
```

#### MlModel
```
MlModel : BaseEntity
├── MarketCode        : string         — Which market this model covers
├── ModelVersion      : int            — Sequential version number
├── TrainedAt         : DateTime
├── TrainingDataHash  : string         — SHA256 of training feature set
├── TrainingSamples   : int            — Number of trades used
├── FeatureVersion    : int            — Which feature schema version
├── Auc               : decimal        — Area Under Curve
├── Precision         : decimal
├── Recall            : decimal
├── F1Score           : decimal
├── CalibrationError  : decimal        — Expected calibration error
├── TopFeaturesJson   : string         — Top 10 features by importance
├── HyperparamsJson   : string         — LightGBM hyperparameters used
├── ModelFilePath     : string         — Path to .zip model file
├── IsActive          : bool           — Currently serving predictions
├── DeactivatedAt     : DateTime?
└── DeactivationReason: string?
```

#### TournamentEntry
```
TournamentEntry : BaseEntity
├── TournamentRunId   : Guid           — FK to TournamentRun
├── StrategyId        : Guid           — FK to Strategy (Backtest context)
├── PaperAccountId    : Guid           — FK to Account (Trading context)
├── MarketCode        : string
├── StartDate         : DateTime
├── DaysActive        : int
├── TotalTrades       : int
├── WinRate           : decimal
├── SharpeRatio       : decimal
├── MaxDrawdown       : decimal
├── TotalReturn       : decimal
├── Status            : TournamentStatus — Active, Promoted, Retired, Paused
├── PromotedAt        : DateTime?
├── RetiredAt         : DateTime?
├── AllocationPercent : decimal         — 0-100 (25 → 50 → 100 ramp)
└── RetirementReason  : string?
```

#### TradeReview
```
TradeReview : BaseEntity
├── TradeId           : Guid           — FK to Order/TradeExecution
├── Symbol            : string
├── MarketCode        : string
├── ReviewDate        : DateTime
├── OutcomeClass      : OutcomeClass   — GoodEntryGoodExit, GoodEntryBadExit,
│                                         BadEntry, RegimeMismatch,
│                                         StoppedCorrectly, StoppedPrematurely
├── MistakeType       : MistakeType?   — BadSignal, BadTiming, RegimeMismatch,
│                                         StopTooTight, StopTooLoose,
│                                         OversizedPosition, CorrelatedLoss,
│                                         BlackSwan (null for winners)
├── ClaudeAnalysis    : string         — Full Claude analysis text
├── Recommendations   : string         — JSON array of recommended actions
├── FeatureSnapshotId : Guid           — FK to FeatureSnapshot
├── RegimeAtEntry     : RegimeType
├── RegimeAtExit      : RegimeType
├── StrategyId        : Guid
└── PnlPercent        : decimal
```

#### StrategyDecayAlert
```
StrategyDecayAlert : BaseEntity
├── StrategyId        : Guid
├── MarketCode        : string
├── AlertType         : DecayAlertType  — Warning (Sharpe<50% avg), Severe (Sharpe<0)
├── RollingSharpe30d  : decimal
├── RollingSharpe60d  : decimal
├── RollingSharpe90d  : decimal
├── HistoricalAvgSharpe: decimal
├── DetectedAt        : DateTime
├── ClaudeAnalysis    : string?         — Populated after Claude reviews
├── RecommendedAction : string?         — "tighten stops", "reduce allocation", "retire"
├── IsResolved        : bool
└── ResolvedAt        : DateTime?
```

---

## 5. System Components

### Component 1: Regime Classifier

**Purpose:** Classify each market into Bull/Bear/Sideways/HighVol daily

**Responsibilities:**
- Compute breadth indicators from universe data
- Apply classification rules (configurable thresholds per market)
- Detect regime transitions
- Store regime history
- Publish `RegimeChanged` event on transitions

**Interfaces:**
- `ClassifyRegimeCommand` → Wolverine handler
- `GetCurrentRegimeQuery` → returns current regime per market
- `GetRegimeHistoryQuery` → returns historical regime series

**Dependencies:**
- MarketDataDbContext (reads PriceCandles, StockUniverse)
- IntelligenceDbContext (writes MarketRegime, BreadthSnapshot)

**FRs Addressed:** FR-001, FR-004

**Classification Logic:**
```
RegimeType Classify(market):
    IF vixLevel > highVolThreshold → HighVolatility
    IF sma50Slope > 0 AND sma200Slope > 0 AND pctAbove200 > 60% → Bull
    IF sma50Slope < 0 AND sma200Slope < 0 AND pctAbove200 < 40% → Bear
    ELSE → Sideways

    Thresholds are per-market (stored in MarketProfile or config):
      US:    highVol=30, bullBreadth=60%, bearBreadth=40%
      India: highVol=25 (India VIX), bullBreadth=55%, bearBreadth=35%
```

---

### Component 2: Strategy Orchestrator

**Purpose:** Select and assign strategies based on current regime

**Responsibilities:**
- Maintain regime → strategy performance matrix
- On RegimeChanged event, select best strategy per market
- Manage gradual switchover (50% → 100% over 5 days)
- Support manual override

**Interfaces:**
- `RegimeChanged` event handler (automatic)
- `AssignStrategyCommand` → manual override
- `GetActiveStrategiesQuery` → current assignments

**Dependencies:**
- IntelligenceDbContext (reads/writes StrategyAssignment)
- BacktestDbContext (reads Strategy, OptimizedParameterSets)

**FRs Addressed:** FR-008, FR-009

**Event Cascade:**
```
RegimeChanged
  → SelectStrategiesHandler
    → reads regime-performance matrix
    → selects best strategy per market for new regime
    → writes StrategyAssignment
    → returns StrategyAssigned event
      → notifies frontend via SignalR (future) or polling
```

---

### Component 3: Daily Pipeline Orchestrator

**Purpose:** Coordinate the end-of-day processing pipeline

**Responsibilities:**
- Orchestrate sequential steps: data ingest → breadth → regime → screen → ML score → grade → orders
- Track pipeline status and timing
- Retry failed steps
- Circuit breaker integration (skip order generation if breaker tripped)

**Implementation:** `DailyPipelineService : BackgroundService`

**Pipeline Sequence:**
```
1. Data Ingestion        — Fetch latest candles for all markets
2. Breadth Computation   — Compute breadth indicators per universe
3. Regime Classification — Classify all markets (may trigger RegimeChanged)
4. Strategy Selection    — Ensure correct strategy assigned per regime
5. Screener Run          — Run active strategy per market
6. ML Scoring            — Score each signal with ML confidence
7. Enhanced Grading      — 7-factor confidence grade (existing 6 + ML)
8. Risk Checks           — Kelly sizing, correlation, circuit breaker
9. Order Generation      — Create paper/live orders for A/B grade signals
10. Feature Capture      — Snapshot indicators for new trades
```

**Schedule:** Triggers at configurable time per market (US: 21:00 UTC, India: 16:30 IST)

**FRs Addressed:** FR-001, FR-004, FR-008, FR-013, FR-015–019

---

### Component 4: ML Pipeline

**Purpose:** Train, validate, deploy, and serve ML trade outcome predictions

**Responsibilities:**
- Feature extraction from FeatureSnapshot table
- Walk-forward model training (ML.NET LightGBM)
- Model validation (AUC threshold, auto-rollback)
- Serve predictions (in-process, < 1ms)
- Feature drift detection
- Model versioning and registry

**Sub-components:**

#### 4a. Feature Extractor
```csharp
public static class FeatureExtractor
{
    // Extracts 40+ features from CandleWithIndicators + regime + context
    public static FeatureVector Extract(
        CandleWithIndicators bar,
        RegimeType regime,
        string marketCode,
        decimal portfolioHeat,
        int recentWinStreak,
        int daysSinceRegimeChange)
    {
        return new FeatureVector
        {
            Rsi = bar.Indicators.Rsi,
            MacdHistogram = bar.Indicators.MacdHistogram,
            SmaShort = bar.Indicators.SmaShort,
            // ... all indicator values
            Regime = (int)regime,
            DaysSinceRegimeChange = daysSinceRegimeChange,
            PortfolioHeat = portfolioHeat,
            RecentWinStreak = recentWinStreak,
            RelativeVolume = bar.Indicators.RelativeVolume,
            BollingerPercentB = bar.Indicators.BollingerPercentB,
            // ... 40+ total features
        };
    }
}
```

#### 4b. Model Trainer
```csharp
public static class MlModelTrainer
{
    public static TrainingResult Train(
        MLContext mlContext,
        IDataView trainingData,
        IDataView validationData,
        LightGbmHyperparams hyperparams)
    {
        var pipeline = mlContext.Transforms
            .Concatenate("Features", featureColumnNames)
            .Append(mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "TradeOutcome",
                featureColumnName: "Features",
                numberOfLeaves: hyperparams.NumLeaves,
                learningRate: hyperparams.LearningRate,
                numberOfIterations: hyperparams.NumIterations));

        var model = pipeline.Fit(trainingData);
        var metrics = mlContext.BinaryClassification.Evaluate(model.Transform(validationData));

        return new TrainingResult
        {
            Model = model,
            Auc = metrics.AreaUnderRocCurve,
            Precision = metrics.PositivePrecision,
            Recall = metrics.PositiveRecall,
            // ...
        };
    }
}
```

#### 4c. Prediction Engine (Singleton Cache)
```csharp
public class MlPredictionService
{
    private readonly ConcurrentDictionary<string, PredictionEngine<FeatureVector, TradePrediction>> _engines;

    // Loaded on startup, reloaded on model retrain
    public decimal PredictConfidence(string marketCode, FeatureVector features)
    {
        if (!_engines.TryGetValue(marketCode, out var engine))
            return 0.5m; // Neutral if no model

        var prediction = engine.Predict(features);
        return (decimal)prediction.Probability; // 0.0 - 1.0
    }
}
```

**FRs Addressed:** FR-011, FR-012, FR-013, FR-014

**ML.NET NuGet Packages:**
```xml
<PackageReference Include="Microsoft.ML" Version="4.*" />
<PackageReference Include="Microsoft.ML.LightGbm" Version="4.*" />
```

---

### Component 5: Tournament Manager

**Purpose:** Run multiple strategies in paper trading and manage promotion/retirement lifecycle

**Responsibilities:**
- Create paper accounts for tournament entries
- Execute daily signals per tournament strategy
- Track performance metrics per entry
- Evaluate promotion criteria (30 days, Sharpe > 1.0, DD < 10%, WR > 50%)
- Evaluate retirement criteria (Sharpe < 0.3 over 60d, or 3 losing months)
- Manage capital ramp (25% → 50% → 100%)
- Ensemble voting across active strategies

**Implementation:** `TournamentManagerService : BackgroundService`

**FRs Addressed:** FR-020, FR-021, FR-022, FR-030

**Lifecycle:**
```
Claude generates strategy
  → Backtest validation (Sharpe > 0.5 on walk-forward)
  → Enter Tournament (paper account created)
  → 30+ day evaluation (daily P&L, Sharpe, DD tracked)
  → If meets criteria: Promote (25% allocation)
  → Ramp: 10 wins or 2 weeks → 50%; 25 wins or 4 weeks → 100%
  → Ongoing monitoring: decay detection
  → If decaying: Claude autopsy → adapt or retire
  → Retired: archived with full history
```

---

### Component 6: Claude Analysis Service

**Purpose:** Asynchronous Claude API integration for all AI analysis tasks

**Responsibilities:**
- Process analysis request queue (Wolverine commands)
- Strategy generation from natural language
- Trade review and classification
- Monthly autopsy for losing strategies
- Rule discovery from trade patterns
- Market DNA profile generation

**Implementation:** `ClaudeAnalysisService : BackgroundService`

**Queue Pattern:**
```
Wolverine Commands (enqueued by other components):
  → GenerateStrategyCommand { Description, MarketCode, TargetRegime }
  → ReviewTradeCommand { TradeId, FeatureSnapshotId }
  → RunAutopsyCommand { StrategyId, Month, Year }
  → DiscoverRulesCommand { MarketCode, MinTrades }
  → GenerateMarketProfileCommand { MarketCode }

Each command has a dedicated handler that:
  1. Calls Claude API with structured prompt
  2. Parses response into domain objects
  3. Saves results to IntelligenceDbContext
  4. Returns completion event (e.g., TradeReviewed, StrategyGenerated)
```

**Claude Prompt Templates:**

Each analysis type has a structured prompt template stored as embedded resources. Example for trade review:
```
System: You are a quantitative trading analyst reviewing a closed trade.

Context:
- Symbol: {symbol}, Market: {market}
- Entry: {entry_price} on {entry_date}, Exit: {exit_price} on {exit_date}
- P&L: {pnl_percent}%
- Regime at entry: {regime_entry}, Regime at exit: {regime_exit}
- Strategy: {strategy_name}
- Indicators at entry: {indicators_json}
- ML confidence at entry: {ml_confidence}

Classify this trade and provide analysis:
1. Outcome class: [GoodEntryGoodExit|GoodEntryBadExit|BadEntry|RegimeMismatch|StoppedCorrectly|StoppedPrematurely]
2. If losing, mistake type: [BadSignal|BadTiming|RegimeMismatch|StopTooTight|StopTooLoose|OversizedPosition|CorrelatedLoss|BlackSwan]
3. What worked well (2-3 bullet points)
4. What went wrong (2-3 bullet points)
5. Recommended adjustments (1-2 specific, actionable suggestions)

Respond in JSON format.
```

**FRs Addressed:** FR-006, FR-007, FR-010, FR-002, FR-023

**Rate Limiting:** Max 50 Claude API calls per daily cycle (trades + autopsy + generation). Calls are prioritized: trade reviews first, then autopsy, then strategy generation.

---

### Component 7: Risk Manager

**Purpose:** Centralized risk checks applied before any order execution

**Responsibilities:**
- Kelly criterion position sizing
- Volatility-targeted share count
- Correlation check against existing portfolio
- Circuit breaker evaluation
- Geographic risk budget enforcement
- Final position size = min(Kelly, vol-target, correlation-adjusted, remaining budget)

**Implementation:** Static utility class called from order generation pipeline

```csharp
public static class RiskManager
{
    public static RiskDecision Evaluate(
        RiskContext context,  // Portfolio state, open positions, regime
        SignalReport signal,  // Screener output with grade + ML score
        CostProfile costs)   // Market-specific transaction costs
    {
        // 1. Circuit breaker check
        if (context.DrawdownFromPeak >= context.CircuitBreakerThreshold)
            return RiskDecision.Blocked("Circuit breaker active");

        // 2. Geographic budget check
        if (context.MarketAllocation[signal.MarketCode] >= context.MaxPerMarket)
            return RiskDecision.Blocked("Geographic budget exceeded");

        // 3. Correlation check
        var avgCorrelation = ComputeAvgCorrelation(signal.Symbol, context.OpenPositions);
        if (avgCorrelation > 0.7m)
            return RiskDecision.Blocked("Correlation too high");

        // 4. Kelly sizing
        var kellyFraction = ComputeKelly(context.RollingWinRate, context.AvgWinLossRatio);
        var kellySize = context.PortfolioValue * kellyFraction * 0.5m; // half-Kelly

        // 5. Volatility targeting
        var targetRisk = Math.Min(kellySize, context.MaxRiskPerTrade);
        var shares = (int)(targetRisk / (signal.Atr * context.AtrMultiplier));

        // 6. Apply cost model
        var estimatedCosts = costs.EstimateRoundTrip(signal.EntryPrice, shares);

        return RiskDecision.Approved(shares, targetRisk, estimatedCosts);
    }
}
```

**FRs Addressed:** FR-015, FR-016, FR-017, FR-018, FR-019, FR-028

---

### Component 8: Feedback Engine

**Purpose:** Continuous monitoring, decay detection, pattern extraction

**Responsibilities:**
- Track rolling strategy metrics (30/60/90-day windows)
- Detect strategy decay (Sharpe below threshold)
- Trigger Claude autopsy on losing months
- Aggregate mistake taxonomy
- Monthly performance attribution
- Pattern extraction every 50 trades

**Wolverine Event Handlers:**
```
TradeClosed
  → CaptureFeatureSnapshotHandler (saves indicator state)
  → UpdateRollingMetricsHandler (updates strategy rolling Sharpe, WR, etc.)
  → CheckDecayHandler (compares rolling vs historical; triggers DecayDetected if needed)

Every 50th trade per market:
  → TriggerPatternExtractionHandler
  → Enqueues DiscoverRulesCommand for Claude

Monthly (1st of month):
  → RunAttributionHandler (decomposes last month's returns)
  → If strategy return < 0: enqueues RunAutopsyCommand for Claude
```

**FRs Addressed:** FR-023, FR-024, FR-025, FR-026, FR-027

---

### Component 9: Enhanced Confidence Grader

**Purpose:** Extend existing 6-factor grader with ML confidence as 7th factor

**Implementation:** Modify existing `ConfidenceGrader.cs` — add ML factor when model is available

```
Factor Weights (with ML active):
  TrendAlignment:    22%  (was 25%)
  Confirmations:     22%  (was 25%)
  Volume:            13%  (was 15%)
  RiskReward:        13%  (was 15%)
  History:            8%  (was 10%)
  Volatility:         7%  (was 10%)
  ML Confidence:     15%  (NEW)
  ─────────────────────
  Total:            100%

Factor Weights (ML not available — < 200 trades):
  Original 6-factor weights unchanged
```

**FRs Addressed:** FR-013

---

## 6. Data Architecture

### 6.1 Data Flow — Daily Pipeline

```
Market Close
    │
    ▼
┌─────────────────────┐
│  Data Ingestion      │  Yahoo Finance API / market data provider
│  (candles per market)│  → PriceCandle (MarketDataDb)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Breadth Computation │  Reads: all PriceCandles for universe
│                      │  → BreadthSnapshot (IntelligenceDb)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Regime Classifier   │  Reads: BreadthSnapshot, PriceCandles (SMA/VIX)
│                      │  → MarketRegime (IntelligenceDb)
│                      │  → RegimeChanged event (if transition)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Strategy Selector   │  Reads: MarketRegime, StrategyAssignment, Strategy
│  (if regime changed) │  → StrategyAssignment (IntelligenceDb)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Indicator Compute   │  Reads: PriceCandles (multi-timeframe)
│  (IndicatorOrches.)  │  → CandleWithIndicators (in-memory)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Screener Engine     │  Reads: CandleWithIndicators, active Strategy
│                      │  → raw signals (in-memory)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  ML Scoring          │  Reads: raw signal features, MlModel
│  (MlPredictionSvc)   │  → ML confidence score per signal (in-memory)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Enhanced Grading    │  Reads: signals + ML scores
│  (ConfidenceGrader)  │  → graded signals A-F (in-memory)
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Risk Manager        │  Reads: Portfolio, Positions, Correlations, Regime
│                      │  → approved trades with position sizes
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  Order Generation    │  → Order + TradeExecution (TradingDb)
│                      │  → FeatureSnapshot (IntelligenceDb)
│                      │  → ScreenerRun (MarketDataDb)
└─────────────────────┘
```

### 6.2 Cross-Database Coordination

Following the existing Wolverine tuple-cascading pattern for cross-DB writes:

```
DailyPipelineService (orchestrator)
  → calls handlers via IMessageBus.InvokeAsync()
  → each handler writes to its own DbContext
  → returns cascading events for next step

Example: Order placed after screening
  SaveScreenerResultHandler     → writes ScreenerRun to MarketDataDb
    returns (ScreenerCompleted)
  GenerateOrdersHandler         → reads signals, runs RiskManager
    → writes Orders to TradingDb
    returns (OrdersGenerated)
  CaptureFeatureSnapshotsHandler → writes FeatureSnapshots to IntelligenceDb
```

### 6.3 Entity Relationships (Cross-Context References)

Since entities live in different DbContexts, cross-references use **Guid IDs** (not navigation properties):

```
IntelligenceDb.FeatureSnapshot.TradeId  ───references──→  TradingDb.Order.Id
IntelligenceDb.TournamentEntry.StrategyId ──references──→  BacktestDb.Strategy.Id
IntelligenceDb.TournamentEntry.PaperAccountId ─refers──→  TradingDb.Account.Id
IntelligenceDb.TradeReview.TradeId ────references──────→  TradingDb.Order.Id
IntelligenceDb.StrategyDecayAlert.StrategyId ──refers──→  BacktestDb.Strategy.Id
```

No foreign key constraints across databases. Application logic ensures consistency via Wolverine event handlers.

### 6.4 Database Indexes (IntelligenceDb)

```sql
-- MarketRegime: fast lookup by market
CREATE INDEX IX_MarketRegime_MarketCode ON MarketRegime(MarketCode);

-- FeatureSnapshot: ML training queries
CREATE INDEX IX_FeatureSnapshot_MarketCode_Outcome ON FeatureSnapshot(MarketCode, TradeOutcome);
CREATE INDEX IX_FeatureSnapshot_TradeId ON FeatureSnapshot(TradeId);

-- MlModel: active model per market
CREATE UNIQUE INDEX IX_MlModel_MarketCode_Active ON MlModel(MarketCode) WHERE IsActive = 1;

-- TournamentEntry: tournament queries
CREATE INDEX IX_TournamentEntry_Status_MarketCode ON TournamentEntry(Status, MarketCode);
CREATE INDEX IX_TournamentEntry_StrategyId ON TournamentEntry(StrategyId);

-- TradeReview: analysis queries
CREATE INDEX IX_TradeReview_MistakeType ON TradeReview(MistakeType);
CREATE INDEX IX_TradeReview_StrategyId ON TradeReview(StrategyId);

-- StrategyDecayAlert: active alerts
CREATE INDEX IX_StrategyDecayAlert_IsResolved ON StrategyDecayAlert(IsResolved, StrategyId);
```

---

## 7. API Design — New Endpoints

All new endpoints follow the existing `IEndpoint` pattern with auto-discovery.

### Intelligence Endpoints (IntelligenceEndpoints.cs)

```
GET  /api/intelligence/regime/{marketCode}              — Current regime for a market
GET  /api/intelligence/regime/{marketCode}/history       — Regime history (paginated)
GET  /api/intelligence/breadth/{marketCode}              — Latest breadth snapshot
GET  /api/intelligence/correlations                      — Current correlation matrix
GET  /api/intelligence/market-profile/{marketCode}       — Market DNA profile
```

### Tournament Endpoints (TournamentEndpoints.cs)

```
GET  /api/tournament/leaderboard/{marketCode}            — Tournament standings
POST /api/tournament/enter                               — Manually enter strategy into tournament
POST /api/tournament/promote/{entryId}                   — Manually promote a strategy
POST /api/tournament/retire/{entryId}                    — Manually retire a strategy
GET  /api/tournament/entries/{entryId}                   — Entry details + performance
GET  /api/tournament/active-strategies/{marketCode}      — Currently active strategies per market
```

### ML Endpoints (MlEndpoints.cs)

```
GET  /api/ml/models/{marketCode}                         — Model registry for a market
GET  /api/ml/models/{marketCode}/active                  — Active model details + metrics
GET  /api/ml/feature-importance/{marketCode}             — Top features by importance
POST /api/ml/retrain/{marketCode}                        — Manually trigger retraining
GET  /api/ml/predictions/{tradeId}                       — ML prediction for a specific trade
```

### Feedback Endpoints (FeedbackEndpoints.cs)

```
GET  /api/feedback/trade-reviews                         — Paginated trade reviews
GET  /api/feedback/trade-reviews/{tradeId}               — Specific trade review
GET  /api/feedback/decay-alerts                          — Active decay alerts
GET  /api/feedback/attribution/{marketCode}/{year}/{month} — Monthly attribution
GET  /api/feedback/mistake-summary/{marketCode}          — Mistake taxonomy breakdown
POST /api/feedback/generate-strategy                     — Ask Claude to generate a strategy
GET  /api/feedback/pipeline-status                       — Daily pipeline run status
```

**Total new endpoints: ~20** (on top of existing 26 = ~46 total)

---

## 8. NFR Coverage

### NFR-001: Backtest Performance

**Requirement:** Single-symbol 5-year < 5s; 500-symbol screen < 60s; walk-forward < 10min

**Solution:**
- Backtest engine stays in-process (no network hops)
- Indicators pre-computed once, reused across walk-forward windows
- Grid search parallelized via `Parallel.ForEach` with `MaxDegreeOfParallelism = Environment.ProcessorCount`
- Tournament daily evaluation reuses screener output (not full backtest per strategy)

**Validation:** Benchmark tests with `Stopwatch` assertions in test suite

---

### NFR-002: ML Inference Latency

**Requirement:** Single prediction < 10ms; 500 signals < 2s

**Solution:**
- ML.NET `PredictionEngine` singleton per market (created once, reused)
- In-process inference — zero serialization, zero network
- Model loaded from `.zip` file at startup and on retrain
- Feature vector is a C# class — direct property access, no dictionary lookups

**Validation:** Benchmark test with 1000 predictions; assert p99 < 5ms

---

### NFR-003: Data Integrity

**Requirement:** Immutable trade journal; indicator snapshots with hash

**Solution:**
- `FeatureSnapshot` and `TradeReview` tables are append-only (no UPDATE/DELETE handlers exposed)
- `FeatureSnapshot.FeaturesJson` stored with SHA256 hash in a separate column for tamper detection
- `MlModel.TrainingDataHash` = SHA256 of the training dataset for reproducibility
- All regime transitions logged in `RegimeTransition` table (append-only)

**Validation:** Integration test: attempt UPDATE on FeatureSnapshot; verify it's blocked at application level

---

### NFR-004: System Reliability

**Requirement:** Background services resilient to transient failures

**Solution:**
- Each pipeline step wrapped in try/catch with structured logging
- Failed steps recorded in `PipelineRunLog` with error details
- Retry policy: 3 attempts with exponential backoff (1s, 5s, 15s)
- Health check endpoint (`/health`) reports status of all 5 background services
- Dead letter: if step fails 3x, skip it, continue pipeline, alert

**Validation:** Health check returns degraded status when a service is unhealthy

---

### NFR-005: Security

**Requirement:** Trading ops require auth; promotion requires confirmation; API keys secure

**Solution:**
- All new endpoints use `.RequireAuthorization()` (existing JWT)
- Tournament promotion endpoint requires explicit `ConfirmPromotion` flag in request body
- Claude API key stored in `appsettings.json` (gitignored) or environment variable `ANTHROPIC_API_KEY`
- ML model files stored in `data/models/` (local filesystem, not exposed via API)

**Validation:** Attempt unauthenticated calls to new endpoints; verify 401

---

### NFR-006: Extensibility — New Markets

**Requirement:** Add market via config, not code

**Solution:**
- `MarketProfile` entity stores per-market configuration:
  ```json
  {
    "marketCode": "UK_FTSE100",
    "exchange": "LSE",
    "tradingHours": { "open": "08:00", "close": "16:30", "timezone": "Europe/London" },
    "currency": "GBP",
    "costProfile": { "commissionPerShare": 0.01, "spreadEstimate": 0.001 },
    "regimeThresholds": { "highVol": 28, "bullBreadth": 0.58, "bearBreadth": 0.38 },
    "vixSymbol": "VFTSE",
    "dataSource": "yahoo"
  }
  ```
- Regime classifier reads thresholds from MarketProfile (not hardcoded)
- StockUniverse already supports arbitrary symbol lists
- Adding a market = create MarketProfile + create StockUniverse + seed candles

**Validation:** Add a test market with 10 symbols; verify full pipeline runs

---

### NFR-007: ML Model Governance

**Requirement:** Versioned, auditable, auto-rollback

**Solution:**
- `MlModel` entity stores full training metadata
- Model files at `data/models/{marketCode}/v{version}.zip`
- Only one model active per market (`IsActive` flag with unique filtered index)
- Retraining: train new model → evaluate on holdout → if AUC > current model's AUC, swap active flag; else keep old
- Feature drift: compare current feature distributions vs training distributions using Kolmogorov-Smirnov test

**Validation:** Retrain with intentionally bad data; verify old model stays active

---

### NFR-008: Observability

**Requirement:** Dashboard showing all autonomous decisions

**Solution:**
- `PipelineRunLog` table: tracks each daily pipeline step (step name, status, duration, error)
- Every autonomous decision logged with reasoning:
  - RegimeTransition: old regime, new regime, triggering indicators
  - StrategyAssignment: strategy chosen, why (Sharpe in this regime)
  - TournamentPromotion: metrics at promotion time
  - CircuitBreakerTrip: drawdown at trip time
- Dashboard API endpoint: `GET /api/feedback/pipeline-status` returns latest run with all steps
- Future: SignalR push for real-time dashboard updates

**Validation:** Run daily pipeline; verify all 10 steps logged with timing

---

## 9. Scalability & Performance

### Current Scale (SQLite)
- Single user, single machine
- 5-10 markets, 500-2000 stocks per universe
- < 100 trades per month
- < 20 tournament strategies concurrently

This is well within SQLite's capabilities. No need for PostgreSQL migration until:
- Multi-user / SaaS requirements emerge
- Trade volume exceeds 10,000/month
- Concurrent API users exceed 50

### Future Migration Path (if needed)
- All DbContexts use EF Core — swap SQLite provider for Npgsql (PostgreSQL)
- Wolverine supports PostgreSQL transport (enables `ScheduleAsync`, durable outbox)
- ML model storage moves to blob storage (S3/Azure Blob)
- Background services move to separate worker process

### Performance Optimizations
- **Indicator computation:** Cached in memory for duration of pipeline run; discarded after
- **ML prediction:** PredictionEngine is thread-safe singleton; zero allocation per prediction
- **Screener:** Parallelizable per symbol (`Parallel.ForEach`)
- **Tournament:** Each strategy's paper trades are independent; parallelizable
- **Data compression:** Large JSON fields (equity curves, feature snapshots) gzipped before storage

---

## 10. Testing Strategy

### Unit Tests (Target: 80%+ coverage on new code)
- **Regime Classifier:** Test each regime classification with synthetic breadth data
- **Risk Manager:** Test Kelly sizing, correlation blocking, circuit breaker logic
- **Feature Extractor:** Test feature vector generation from known indicator states
- **Enhanced Grader:** Test 7-factor grading with and without ML factor
- **Tournament criteria:** Test promotion/retirement logic with edge cases

### Integration Tests
- **Daily Pipeline:** End-to-end pipeline with seeded test data
- **ML Pipeline:** Train model on synthetic data, verify prediction output shape
- **Cross-DB coordination:** Verify FeatureSnapshot created when Order placed
- **Claude mock:** Mock Anthropic SDK; verify prompt construction and response parsing

### Test Infrastructure (Existing Patterns)
- `TestDbContextFactory.Create()` for in-memory DbContext
- `FakeCurrentUser` for auth bypass
- `NSubstitute` for mocking external services (Claude SDK, data providers)
- New: `FakeMlPredictionService` returning configurable confidence scores
- New: `TestIntelligenceDbContextFactory.Create()`

---

## 11. Project Structure (New Files)

```
src/
├── TradingAssistant.Domain/
│   └── Intelligence/              ← NEW bounded context
│       ├── MarketRegime.cs
│       ├── RegimeTransition.cs
│       ├── MarketProfile.cs
│       ├── CorrelationSnapshot.cs
│       ├── BreadthSnapshot.cs
│       ├── TournamentRun.cs
│       ├── TournamentEntry.cs
│       ├── StrategyAssignment.cs
│       ├── FeatureSnapshot.cs
│       ├── MlModel.cs
│       ├── MlPrediction.cs
│       ├── TradeReview.cs
│       ├── StrategyDecayAlert.cs
│       ├── PerformanceAttribution.cs
│       ├── CostProfile.cs
│       ├── PipelineRunLog.cs
│       └── Enums/
│           ├── RegimeType.cs
│           ├── TournamentStatus.cs
│           ├── OutcomeClass.cs
│           ├── MistakeType.cs
│           └── DecayAlertType.cs
│
├── TradingAssistant.Contracts/
│   ├── Intelligence/              ← NEW
│   │   ├── Commands/
│   │   │   ├── ClassifyRegimeCommand.cs
│   │   │   ├── RunTournamentCycleCommand.cs
│   │   │   ├── EnterTournamentCommand.cs
│   │   │   ├── PromoteStrategyCommand.cs
│   │   │   ├── RetireStrategyCommand.cs
│   │   │   ├── RetrainModelCommand.cs
│   │   │   ├── GenerateStrategyCommand.cs
│   │   │   ├── ReviewTradeCommand.cs
│   │   │   ├── RunAutopsyCommand.cs
│   │   │   └── DiscoverRulesCommand.cs
│   │   ├── Queries/
│   │   │   ├── GetCurrentRegimeQuery.cs
│   │   │   ├── GetRegimeHistoryQuery.cs
│   │   │   ├── GetTournamentLeaderboardQuery.cs
│   │   │   ├── GetActiveStrategiesQuery.cs
│   │   │   ├── GetMlModelQuery.cs
│   │   │   ├── GetFeatureImportanceQuery.cs
│   │   │   ├── GetTradeReviewsQuery.cs
│   │   │   ├── GetDecayAlertsQuery.cs
│   │   │   ├── GetAttributionQuery.cs
│   │   │   ├── GetMistakeSummaryQuery.cs
│   │   │   └── GetPipelineStatusQuery.cs
│   │   ├── Events/
│   │   │   ├── RegimeChanged.cs
│   │   │   ├── StrategyAssigned.cs
│   │   │   ├── StrategyPromoted.cs
│   │   │   ├── StrategyRetired.cs
│   │   │   ├── ModelTrained.cs
│   │   │   ├── TradeReviewed.cs
│   │   │   ├── DecayDetected.cs
│   │   │   └── PipelineStepCompleted.cs
│   │   └── Dtos/
│   │       ├── MarketRegimeDto.cs
│   │       ├── BreadthSnapshotDto.cs
│   │       ├── CorrelationMatrixDto.cs
│   │       ├── MarketProfileDto.cs
│   │       ├── TournamentEntryDto.cs
│   │       ├── TournamentLeaderboardDto.cs
│   │       ├── MlModelDto.cs
│   │       ├── FeatureImportanceDto.cs
│   │       ├── TradeReviewDto.cs
│   │       ├── DecayAlertDto.cs
│   │       ├── AttributionDto.cs
│   │       ├── MistakeSummaryDto.cs
│   │       ├── RiskDecisionDto.cs
│   │       └── PipelineStatusDto.cs
│
├── TradingAssistant.Application/
│   ├── Intelligence/              ← NEW
│   │   ├── RegimeClassifier.cs
│   │   ├── BreadthCalculator.cs
│   │   ├── CorrelationCalculator.cs
│   │   ├── StrategySelector.cs
│   │   ├── RiskManager.cs
│   │   ├── FeatureExtractor.cs
│   │   ├── MlModelTrainer.cs
│   │   └── PerformanceAttributor.cs
│   ├── Handlers/Intelligence/     ← NEW
│   │   ├── ClassifyRegimeHandler.cs
│   │   ├── SelectStrategiesHandler.cs
│   │   ├── RunTournamentCycleHandler.cs
│   │   ├── EnterTournamentHandler.cs
│   │   ├── PromoteStrategyHandler.cs
│   │   ├── RetireStrategyHandler.cs
│   │   ├── RetrainModelHandler.cs
│   │   ├── CaptureFeatureSnapshotHandler.cs
│   │   ├── CheckDecayHandler.cs
│   │   ├── UpdateRollingMetricsHandler.cs
│   │   ├── GenerateStrategyHandler.cs
│   │   ├── ReviewTradeHandler.cs
│   │   ├── RunAutopsyHandler.cs
│   │   ├── DiscoverRulesHandler.cs
│   │   └── Queries/
│   │       ├── GetCurrentRegimeHandler.cs
│   │       ├── GetRegimeHistoryHandler.cs
│   │       ├── GetTournamentLeaderboardHandler.cs
│   │       ├── GetMlModelHandler.cs
│   │       ├── GetTradeReviewsHandler.cs
│   │       ├── GetDecayAlertsHandler.cs
│   │       └── GetPipelineStatusHandler.cs
│
├── TradingAssistant.Infrastructure/
│   ├── Persistence/
│   │   └── IntelligenceDbContext.cs       ← NEW (4th DbContext)
│   ├── Migrations/Intelligence/           ← NEW
│   ├── ML/                                ← NEW
│   │   ├── MlPredictionService.cs
│   │   └── FeatureVector.cs
│   └── Claude/                            ← NEW
│       ├── ClaudeClientWrapper.cs
│       └── PromptTemplates/
│           ├── TradeReviewPrompt.cs
│           ├── StrategyGenerationPrompt.cs
│           ├── AutopsyPrompt.cs
│           ├── RuleDiscoveryPrompt.cs
│           └── MarketProfilePrompt.cs
│
├── TradingAssistant.Api/
│   ├── Endpoints/
│   │   ├── IntelligenceEndpoints.cs       ← NEW
│   │   ├── TournamentEndpoints.cs         ← NEW
│   │   ├── MlEndpoints.cs                ← NEW
│   │   └── FeedbackEndpoints.cs           ← NEW
│   └── Services/
│       ├── DailyPipelineService.cs        ← NEW
│       ├── TournamentManagerService.cs    ← NEW
│       ├── MlRetrainService.cs            ← NEW
│       └── ClaudeAnalysisService.cs       ← NEW
│
data/
├── models/                                ← NEW (ML model files)
│   ├── US_SP500/
│   │   ├── v1.zip
│   │   └── v2.zip
│   └── IN_NIFTY50/
│       └── v1.zip
├── market.db
├── trading.db
├── backtest.db
└── intelligence.db                        ← NEW
```

---

## 12. Traceability

### FR → Component Mapping

| FR | Name | Components |
|----|------|-----------|
| FR-001 | Regime Classifier | RegimeClassifier, DailyPipelineService, IntelligenceDb |
| FR-002 | Market DNA Profiles | ClaudeAnalysisService, IntelligenceDb |
| FR-003 | Correlation Monitor | CorrelationCalculator, DailyPipelineService |
| FR-004 | Breadth Engine | BreadthCalculator, DailyPipelineService |
| FR-005 | Macro Overlay | RegimeClassifier (extended), MarketProfile config |
| FR-006 | Strategy Generator | ClaudeAnalysisService, GenerateStrategyHandler |
| FR-007 | Rule Discovery | ClaudeAnalysisService, DiscoverRulesHandler |
| FR-008 | Regime-Based Selection | StrategySelector, SelectStrategiesHandler |
| FR-009 | Market Playbooks | ClaudeAnalysisService, MarketProfile |
| FR-010 | Strategy Autopsy | ClaudeAnalysisService, RunAutopsyHandler |
| FR-011 | Feature Store | FeatureExtractor, FeatureSnapshot entity |
| FR-012 | ML Predictor | MlModelTrainer, MlPredictionService |
| FR-013 | ML Confidence Integration | Enhanced ConfidenceGrader |
| FR-014 | Model Retraining | MlRetrainService, RetrainModelHandler |
| FR-015 | Kelly Sizing | RiskManager |
| FR-016 | Correlation Allocation | RiskManager, CorrelationCalculator |
| FR-017 | Circuit Breaker | RiskManager, DailyPipelineService |
| FR-018 | Vol-Target Sizing | RiskManager |
| FR-019 | Geographic Budget | RiskManager |
| FR-020 | Paper Arena | TournamentManagerService, existing Account/Order |
| FR-021 | Promote/Retire | TournamentManagerService, PromoteStrategyHandler |
| FR-022 | Ensemble Voting | TournamentManagerService |
| FR-023 | AI Trade Journal | ClaudeAnalysisService, ReviewTradeHandler |
| FR-024 | Indicator Snapshot | CaptureFeatureSnapshotHandler, FeatureExtractor |
| FR-025 | Decay Monitor | CheckDecayHandler, StrategyDecayAlert |
| FR-026 | Mistake Taxonomy | ReviewTradeHandler, TradeReview entity |
| FR-027 | Performance Attribution | PerformanceAttributor |
| FR-028 | Cost Model | CostProfile entity, RiskManager |
| FR-029 | Data Quality Monitor | DailyPipelineService (step 1 validation) |
| FR-030 | Gradual Deployment | TournamentManagerService (allocation ramp) |

**Coverage: 30/30 FRs mapped to components.**

### NFR → Solution Mapping

| NFR | Name | Solution |
|-----|------|----------|
| NFR-001 | Backtest Performance | In-process computation, parallel grid search |
| NFR-002 | ML Inference Latency | ML.NET PredictionEngine singleton, in-process |
| NFR-003 | Data Integrity | Append-only tables, SHA256 hashes, immutable snapshots |
| NFR-004 | System Reliability | Retry + backoff, health checks, dead-letter logging |
| NFR-005 | Security | JWT auth, explicit confirmation for promotions, env-var API keys |
| NFR-006 | Extensibility | Config-driven MarketProfile, no hardcoded thresholds |
| NFR-007 | ML Governance | MlModel registry, auto-rollback, feature drift detection |
| NFR-008 | Observability | PipelineRunLog, decision audit trail, dashboard endpoints |

**Coverage: 8/8 NFRs addressed.**

---

## 13. Key Trade-offs

### Decision 1: 4th SQLite Database vs. Extending Existing
**Choice:** New `intelligence.db` (IntelligenceDbContext)
- **Gain:** Clean bounded context; intelligence can be wiped/rebuilt without affecting trading/backtest data; independent migrations
- **Lose:** Cross-DB joins impossible; must use Guid references
- **Rationale:** Intelligence data is experimental (regime models may change, ML retraining resets data). Isolation protects production trading data from intelligence experiments.

### Decision 2: ML.NET vs. Python Sidecar
**Choice:** ML.NET with LightGBM trainer
- **Gain:** Zero deployment complexity, < 1ms inference, single process
- **Lose:** Python ecosystem has more ML libraries, easier experimentation
- **Rationale:** Our use case (binary classification, 40 features, < 10K rows) is squarely in ML.NET's sweet spot. If we need deep learning later, we can export ONNX from Python and load in ML.NET.

### Decision 3: Claude Async Batch vs. Synchronous
**Choice:** Async background service with command queue
- **Gain:** Pipeline doesn't block on 2-10s Claude API calls; rate limit friendly; cheaper
- **Lose:** Trade reviews available next day, not immediately
- **Rationale:** No trading decision depends on Claude in real-time. Claude operates at strategic level (generate strategies, review trades, diagnose decay) — all of which tolerate 24h delay.

### Decision 4: Monolith vs. Separate Intelligence Service
**Choice:** Stay monolith, add 4th bounded context
- **Gain:** In-process access to indicators and backtest engine (critical for NFR-001, NFR-002); simpler deployment; Wolverine cascading works
- **Lose:** All components scale together (can't scale ML independently)
- **Rationale:** Single-user system with SQLite. Scaling individual components provides zero benefit currently. Monolith is appropriate until multi-user or high-throughput requirements emerge.

---

## 14. Open Questions Resolution

| # | Question | Resolution |
|---|----------|-----------|
| Q1 | ML runtime for .NET? | **ML.NET 4.x with LightGBM trainer.** Native .NET, in-process inference < 1ms, NuGet package `Microsoft.ML.LightGbm`. |
| Q2 | India data provider? | **Yahoo Finance for daily data** (already in use). Supplement with NSE CSV downloads for historical data gaps. Add data quality monitor (FR-029) to detect missing data. |
| Q3 | Claude sync vs async? | **Async batch.** Background service processes queue post-market. No Claude call blocks the daily pipeline. |
| Q4 | Stock splits/dividends? | **Use adjusted close from Yahoo Finance** (already the default). Data quality monitor (FR-029) detects unadjusted anomalies (>20% gap). Manual override for known events. |
| Q5 | Fixed vs adaptive regime thresholds? | **Config-driven per market via MarketProfile.** Start with fixed thresholds; Claude updates them quarterly as part of Market DNA Profile (FR-002). |

---

*Generated by BMAD Method v6 — System Architect*
*Architecture Session: 2026-03-02*
