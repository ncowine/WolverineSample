namespace TradingAssistant.Contracts.Backtesting;

/// <summary>
/// Rich strategy definition model supporting multi-timeframe conditions,
/// position sizing, and risk management. Serialized as JSON in Strategy.RulesJson.
/// Lives in Contracts to avoid circular dependencies (Contracts has zero project refs).
/// Uses string-based enums for serialization compatibility.
/// </summary>
public class StrategyDefinition
{
    /// <summary>
    /// Entry conditions: ALL groups must be true (AND logic between groups).
    /// Within each group, ANY condition can be true (OR logic within group).
    /// </summary>
    public List<ConditionGroup> EntryConditions { get; set; } = new();

    /// <summary>
    /// Exit conditions: same structure as entry. When met, close position.
    /// </summary>
    public List<ConditionGroup> ExitConditions { get; set; } = new();

    public StopLossConfig StopLoss { get; set; } = new();
    public TakeProfitConfig TakeProfit { get; set; } = new();
    public PositionSizingConfig PositionSizing { get; set; } = new();
    public TradeFilterConfig Filters { get; set; } = new();
}

/// <summary>
/// A group of conditions that are OR'd together. Multiple groups are AND'd.
/// Example: (RSI &lt; 30 OR Stochastic &lt; 20) AND (EMA50 &gt; EMA200 on Weekly).
/// </summary>
public class ConditionGroup
{
    /// <summary>
    /// Timeframe: "Daily", "Weekly", "Monthly".
    /// </summary>
    public string Timeframe { get; set; } = "Daily";

    public List<Condition> Conditions { get; set; } = new();
}

/// <summary>
/// A single indicator condition.
/// </summary>
public class Condition
{
    /// <summary>
    /// Indicator type: RSI, MACD, SMA, EMA, BollingerBands, WMA, Stochastic, ATR, OBV, Price, Volume.
    /// </summary>
    public string Indicator { get; set; } = string.Empty;

    /// <summary>
    /// Comparison: CrossAbove, CrossBelow, GreaterThan, LessThan, Between.
    /// </summary>
    public string Comparison { get; set; } = string.Empty;

    /// <summary>
    /// Threshold value. For CrossAbove/CrossBelow with another indicator, use ReferenceIndicator.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Upper bound for Between comparisons.
    /// </summary>
    public decimal? ValueHigh { get; set; }

    /// <summary>
    /// Indicator period override (e.g. SMA 50 vs SMA 200). Null = use default.
    /// </summary>
    public int? Period { get; set; }

    /// <summary>
    /// Reference indicator for crossover comparisons (e.g. SMA crosses above EMA).
    /// </summary>
    public string? ReferenceIndicator { get; set; }

    /// <summary>
    /// Period for the reference indicator.
    /// </summary>
    public int? ReferencePeriod { get; set; }
}

public class StopLossConfig
{
    /// <summary>
    /// Type: Atr, FixedPercent, Support.
    /// </summary>
    public string Type { get; set; } = "Atr";

    /// <summary>
    /// Multiplier: for ATR = N x ATR, for FixedPercent = N%.
    /// </summary>
    public decimal Multiplier { get; set; } = 2m;

    /// <summary>
    /// Maximum stop-loss distance as % of entry price. Caps ATR-based SL on volatile stocks.
    /// </summary>
    public decimal MaxStopLossPercent { get; set; } = 5m;

    /// <summary>
    /// Enable trailing stop that ratchets up as price moves in our favor.
    /// </summary>
    public bool UseTrailingStop { get; set; }

    /// <summary>
    /// Activate trailing stop after unrealized profit reaches this many R-multiples.
    /// </summary>
    public decimal TrailingActivationR { get; set; } = 1.5m;

    /// <summary>
    /// Trail stop at this many ATR below the highest high since entry.
    /// </summary>
    public decimal TrailingAtrMultiplier { get; set; } = 2m;

    /// <summary>
    /// Enable time-decay tightening: narrow stops on stale positions.
    /// </summary>
    public bool UseTimeDecay { get; set; }

    /// <summary>
    /// Start tightening stops after this many days in the position.
    /// </summary>
    public int TimeDecayStartDays { get; set; } = 10;

    /// <summary>
    /// Tighten stop to this % of the original risk distance.
    /// E.g. 50 = move SL to halfway between entry and original SL.
    /// </summary>
    public decimal TimeDecayTightenPercent { get; set; } = 50m;
}

public class TakeProfitConfig
{
    /// <summary>
    /// Type: RMultiple, FixedPercent, Resistance.
    /// </summary>
    public string Type { get; set; } = "RMultiple";

    /// <summary>
    /// Multiplier: for RMultiple = N x risk, for FixedPercent = N%.
    /// </summary>
    public decimal Multiplier { get; set; } = 2m;
}

public class PositionSizingConfig
{
    /// <summary>
    /// Sizing method: "Fixed" (default) or "Kelly" (Kelly Criterion-based).
    /// </summary>
    public string SizingMethod { get; set; } = "Fixed";

    /// <summary>
    /// Percentage of account to risk per trade (default 1%).
    /// Also serves as the upper cap for Kelly-based sizing.
    /// </summary>
    public decimal RiskPercent { get; set; } = 1m;

    /// <summary>
    /// Maximum simultaneous open positions.
    /// </summary>
    public int MaxPositions { get; set; } = 6;

    /// <summary>
    /// Maximum total portfolio risk (sum of all open position risks).
    /// </summary>
    public decimal MaxPortfolioHeat { get; set; } = 12m;

    /// <summary>
    /// Max drawdown before circuit breaker pauses trading.
    /// </summary>
    public decimal MaxDrawdownPercent { get; set; } = 15m;

    /// <summary>
    /// Resume trading when equity recovers to within this % of peak.
    /// E.g. 5 means resume when equity >= peak * 0.95.
    /// </summary>
    public decimal DrawdownRecoveryPercent { get; set; } = 5m;

    /// <summary>
    /// Kelly fraction multiplier (default 0.5 = half-Kelly for safety).
    /// Only used when SizingMethod = "Kelly".
    /// </summary>
    public decimal KellyMultiplier { get; set; } = 0.5m;

    /// <summary>
    /// Rolling window of closed trades for Kelly calculation.
    /// </summary>
    public int KellyWindowSize { get; set; } = 50;

    /// <summary>
    /// Enable volatility-targeted sizing: shares = riskDollars / (ATR × multiplier).
    /// Layers on top of Kelly or Fixed: risk budget comes from SizingMethod,
    /// vol-targeting converts that budget into share count.
    /// </summary>
    public bool UseVolTargeting { get; set; }

    /// <summary>
    /// ATR multiplier for vol-targeted sizing (default 2.0).
    /// Separate from StopLoss ATR multiplier so sizing and stops can diverge.
    /// </summary>
    public decimal VolTargetAtrMultiplier { get; set; } = 2m;

    /// <summary>
    /// Enable correlation-aware allocation: block or reduce positions
    /// that are highly correlated with existing portfolio holdings.
    /// </summary>
    public bool UseCorrelationFilter { get; set; }

    /// <summary>
    /// Block entry when avg pairwise correlation with open positions exceeds this (default 0.7).
    /// </summary>
    public decimal CorrelationBlockThreshold { get; set; } = 0.7m;

    /// <summary>
    /// Start reducing position size when avg correlation exceeds this (default 0.5).
    /// Size scales linearly from 100% at this threshold to 0% at BlockThreshold.
    /// </summary>
    public decimal CorrelationReduceThreshold { get; set; } = 0.5m;

    /// <summary>
    /// Enable geographic risk budget: block entry if adding a position
    /// would exceed the max allocation for its market/region.
    /// </summary>
    public bool UseGeographicRiskBudget { get; set; }

    /// <summary>
    /// Maximum notional allocation per market as % of total equity (default 50%).
    /// </summary>
    public decimal MaxMarketAllocationPercent { get; set; } = 50m;

    /// <summary>
    /// Maximum holding days before forcing an exit. 0 = unlimited (default).
    /// </summary>
    public int MaxHoldingDays { get; set; } = 20;

    /// <summary>
    /// When true, stale positions (exceeding MaxHoldingDays) are swapped for new
    /// higher-scoring signals rather than simply closed. Only applies when
    /// MaxHoldingDays > 0.
    /// </summary>
    public bool UseOpportunityCostSwap { get; set; }

    /// <summary>
    /// Enable weekly-trend alignment filter: skip long entries when weekly SMA50 &lt; SMA200.
    /// Requires weekly bar data to be present for the symbol.
    /// </summary>
    public bool RequireWeeklyTrendAlignment { get; set; }
}

public class TradeFilterConfig
{
    public long? MinVolume { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string>? Sectors { get; set; }
}
