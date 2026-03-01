namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// A single walk-forward window with in-sample optimization and out-of-sample validation.
/// </summary>
public class WalkForwardWindow
{
    public int WindowNumber { get; init; }
    public DateTime InSampleStart { get; init; }
    public DateTime InSampleEnd { get; init; }
    public DateTime OutOfSampleStart { get; init; }
    public DateTime OutOfSampleEnd { get; init; }

    /// <summary>
    /// Best parameters found during in-sample optimization.
    /// </summary>
    public ParameterSet BestParameters { get; init; } = new();

    /// <summary>
    /// Sharpe ratio achieved on in-sample data.
    /// </summary>
    public decimal InSampleSharpe { get; init; }

    /// <summary>
    /// Sharpe ratio achieved on out-of-sample data using in-sample best params.
    /// </summary>
    public decimal OutOfSampleSharpe { get; init; }

    /// <summary>
    /// Full metrics from out-of-sample test.
    /// </summary>
    public PerformanceMetrics OutOfSampleMetrics { get; init; } = new();

    /// <summary>
    /// Overfitting score for this window: (IS_Sharpe - OOS_Sharpe) / IS_Sharpe.
    /// Lower is better. Negative means OOS outperformed IS.
    /// </summary>
    public decimal OverfittingScore { get; init; }

    /// <summary>
    /// Walk-forward efficiency: OOS_Sharpe / IS_Sharpe. Higher is better (>0.5 = acceptable).
    /// </summary>
    public decimal Efficiency { get; init; }
}

/// <summary>
/// Overfitting grade based on score thresholds.
/// </summary>
public enum OverfittingGrade
{
    Good,    // < 30%
    Warning, // 30-50%
    Overfitted // > 50%
}

/// <summary>
/// Complete walk-forward analysis result.
/// </summary>
public class WalkForwardResult
{
    public WalkForwardConfig Config { get; init; } = new();
    public List<WalkForwardWindow> Windows { get; init; } = new();

    // ── Aggregate Metrics ─────────────────────────────────────

    /// <summary>
    /// Average in-sample Sharpe across all windows.
    /// </summary>
    public decimal AverageInSampleSharpe { get; init; }

    /// <summary>
    /// Average out-of-sample Sharpe across all windows.
    /// </summary>
    public decimal AverageOutOfSampleSharpe { get; init; }

    /// <summary>
    /// Average overfitting score across all windows.
    /// </summary>
    public decimal AverageOverfittingScore { get; init; }

    /// <summary>
    /// Average walk-forward efficiency across all windows.
    /// </summary>
    public decimal AverageEfficiency { get; init; }

    /// <summary>
    /// Overall overfitting grade based on average score.
    /// </summary>
    public OverfittingGrade Grade { get; init; }

    /// <summary>
    /// Blessed parameters: from the window with the best OOS Sharpe.
    /// These performed best on truly unseen data.
    /// </summary>
    public ParameterSet BlessedParameters { get; init; } = new();

    /// <summary>
    /// Aggregated out-of-sample equity curve (concatenated across windows).
    /// </summary>
    public List<EquityPoint> AggregatedEquityCurve { get; init; } = new();

    public TimeSpan ElapsedTime { get; init; }
    public List<string> Warnings { get; init; } = new();
}
