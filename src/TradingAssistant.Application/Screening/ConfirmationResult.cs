namespace TradingAssistant.Application.Screening;

/// <summary>
/// Result of a single confirmation check.
/// </summary>
public record ConfirmationResult
{
    /// <summary>
    /// Name of the confirmation (e.g. "TrendAlignment", "Momentum").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether this confirmation passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Weight of this confirmation in the total score (default 1.0).
    /// </summary>
    public decimal Weight { get; init; } = 1m;

    /// <summary>
    /// Human-readable reason for the result.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Complete signal evaluation result with all confirmations.
/// </summary>
public class SignalEvaluation
{
    public string Symbol { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public SignalDirection Direction { get; init; }

    /// <summary>
    /// Individual confirmation results.
    /// </summary>
    public List<ConfirmationResult> Confirmations { get; init; } = new();

    /// <summary>
    /// Total confirmation score: sum(passed weights) / sum(all weights).
    /// Range: 0.0 to 1.0.
    /// </summary>
    public decimal TotalScore { get; init; }

    /// <summary>
    /// Number of confirmations that passed.
    /// </summary>
    public int PassedCount => Confirmations.Count(c => c.Passed);

    /// <summary>
    /// Total number of confirmations evaluated.
    /// </summary>
    public int TotalCount => Confirmations.Count;
}
