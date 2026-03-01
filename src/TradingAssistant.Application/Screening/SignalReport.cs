namespace TradingAssistant.Application.Screening;

/// <summary>
/// A single factor's contribution to the confidence score.
/// </summary>
public record GradeBreakdownEntry
{
    /// <summary>
    /// Factor name (e.g. "TrendAlignment", "RiskReward").
    /// </summary>
    public string Factor { get; init; } = string.Empty;

    /// <summary>
    /// Raw score for this factor (0-100).
    /// </summary>
    public decimal RawScore { get; init; }

    /// <summary>
    /// Weight of this factor (0.0-1.0, sums to 1.0 across all factors).
    /// </summary>
    public decimal Weight { get; init; }

    /// <summary>
    /// Weighted contribution = RawScore * Weight.
    /// </summary>
    public decimal WeightedScore => RawScore * Weight;

    /// <summary>
    /// Human-readable explanation.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Complete signal report with grade, score breakdown, and trade prices.
/// </summary>
public class SignalReport
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public SignalDirection Direction { get; init; }

    /// <summary>
    /// Final confidence grade (A-F).
    /// </summary>
    public SignalGrade Grade { get; init; }

    /// <summary>
    /// Final confidence score (0-100).
    /// </summary>
    public decimal Score { get; init; }

    /// <summary>
    /// Per-factor breakdown showing how the score was calculated.
    /// </summary>
    public List<GradeBreakdownEntry> Breakdown { get; init; } = new();

    // ── Trade Prices ─────────────────────────────────────────

    /// <summary>
    /// Suggested entry price.
    /// </summary>
    public decimal EntryPrice { get; init; }

    /// <summary>
    /// Suggested stop-loss price.
    /// </summary>
    public decimal StopPrice { get; init; }

    /// <summary>
    /// Suggested take-profit target price.
    /// </summary>
    public decimal TargetPrice { get; init; }

    /// <summary>
    /// Risk-to-reward ratio (distance to target / distance to stop).
    /// </summary>
    public decimal RiskRewardRatio { get; init; }

    /// <summary>
    /// Historical win rate for similar setups (0-100). Null if no history.
    /// </summary>
    public decimal? HistoricalWinRate { get; init; }

    /// <summary>
    /// True if this signal passes the quality gate (A or B grade).
    /// </summary>
    public bool PassesScreener => Grade is SignalGrade.A or SignalGrade.B;

    /// <summary>
    /// Underlying signal evaluation from SignalEvaluator.
    /// </summary>
    public SignalEvaluation Evaluation { get; init; } = new();
}
