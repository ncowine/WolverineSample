namespace TradingAssistant.Application.Backtesting;

/// <summary>
/// Walk-forward window type.
/// </summary>
public enum WalkForwardMode
{
    /// <summary>
    /// Rolling: in-sample window slides forward (fixed size).
    /// </summary>
    Rolling,

    /// <summary>
    /// Anchored: in-sample always starts from the beginning (grows each window).
    /// </summary>
    Anchored
}

/// <summary>
/// Configuration for walk-forward analysis.
/// </summary>
public class WalkForwardConfig
{
    /// <summary>
    /// In-sample window size in days.
    /// Default: ~2 years (504 trading days).
    /// </summary>
    public int InSampleDays { get; init; } = 504;

    /// <summary>
    /// Out-of-sample window size in days.
    /// Default: ~6 months (126 trading days).
    /// </summary>
    public int OutOfSampleDays { get; init; } = 126;

    /// <summary>
    /// Rolling or anchored walk-forward.
    /// </summary>
    public WalkForwardMode Mode { get; init; } = WalkForwardMode.Rolling;

    /// <summary>
    /// Risk-free rate for Sharpe computation.
    /// </summary>
    public decimal RiskFreeRate { get; init; } = 4.5m;

    /// <summary>
    /// Number of top results to keep from each in-sample optimization.
    /// </summary>
    public int OptimizationTopN { get; init; } = 5;
}
