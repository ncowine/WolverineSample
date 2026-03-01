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
    /// Percentage of account to risk per trade (default 1%).
    /// </summary>
    public decimal RiskPercent { get; set; } = 1m;

    /// <summary>
    /// Maximum simultaneous open positions.
    /// </summary>
    public int MaxPositions { get; set; } = 6;

    /// <summary>
    /// Maximum total portfolio risk (sum of all open position risks).
    /// </summary>
    public decimal MaxPortfolioHeat { get; set; } = 6m;

    /// <summary>
    /// Max drawdown before circuit breaker pauses trading.
    /// </summary>
    public decimal MaxDrawdownPercent { get; set; } = 15m;

    /// <summary>
    /// Resume trading when equity recovers to within this % of peak.
    /// E.g. 5 means resume when equity >= peak * 0.95.
    /// </summary>
    public decimal DrawdownRecoveryPercent { get; set; } = 5m;
}

public class TradeFilterConfig
{
    public long? MinVolume { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string>? Sectors { get; set; }
}
