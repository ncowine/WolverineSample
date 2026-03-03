namespace TradingAssistant.Application.Screening;

/// <summary>
/// Grades a trade signal from A-F using weighted multi-factor scoring.
///
/// Factor weights (base 6-factor mode):
///   TF alignment:  25%
///   Confirmations: 25%
///   Volume:        15%
///   R:R ratio:     15%
///   History:       10%
///   Volatility:    10%
///
/// When ML confidence is provided, it becomes a 7th factor at 15% weight
/// and the existing 6 factors are proportionally re-weighted (×0.85) to sum to 100%.
///
/// Each factor scores 0-100, then weighted sum produces final 0-100 score.
/// Grade: A (90+), B (75-89), C (60-74), D (40-59), F (&lt;40).
/// </summary>
public static class ConfidenceGrader
{
    // Base factor weights (must sum to 1.0)
    private const decimal WeightTrendAlignment = 0.25m;
    private const decimal WeightConfirmations = 0.25m;
    private const decimal WeightVolume = 0.15m;
    private const decimal WeightRiskReward = 0.15m;
    private const decimal WeightHistory = 0.10m;
    private const decimal WeightVolatility = 0.10m;

    /// <summary>
    /// ML factor weight when ML confidence is available.
    /// Existing factors are scaled by (1 - MlWeight) to maintain 100% total.
    /// </summary>
    internal const decimal WeightMlConfidence = 0.15m;

    /// <summary>
    /// Grade a signal based on multi-factor confidence scoring.
    /// </summary>
    /// <param name="evaluation">Signal evaluation from <see cref="SignalEvaluator"/>.</param>
    /// <param name="entryPrice">Planned entry price.</param>
    /// <param name="stopPrice">Planned stop-loss price.</param>
    /// <param name="targetPrice">Planned take-profit price.</param>
    /// <param name="historicalWinRate">
    /// Historical win rate for similar setups (0-100).
    /// If null, a neutral value of 50 is used.
    /// </param>
    /// <param name="mlConfidence">
    /// ML model predicted probability (0.0–1.0). When provided, adds ML
    /// as a 7th factor at 15% weight (existing 6 factors scaled by 0.85).
    /// When null, original 6-factor grading is used unchanged.
    /// </param>
    public static SignalReport Grade(
        SignalEvaluation evaluation,
        decimal entryPrice,
        decimal stopPrice,
        decimal targetPrice,
        decimal? historicalWinRate = null,
        float? mlConfidence = null)
    {
        var breakdown = new List<GradeBreakdownEntry>();

        // When ML confidence is available, scale existing weights by (1 - MlWeight)
        var scaleFactor = mlConfidence.HasValue ? (1m - WeightMlConfidence) : 1m;

        // 1. TF alignment (25% base, 21.25% with ML)
        var trendResult = evaluation.Confirmations.FirstOrDefault(c => c.Name == "TrendAlignment");
        var trendScore = trendResult?.Passed == true ? 100m : 0m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "TrendAlignment",
            RawScore = trendScore,
            Weight = WeightTrendAlignment * scaleFactor,
            Reason = trendResult?.Reason ?? "Not evaluated"
        });

        // 2. Confirmations (25% base, 21.25% with ML)
        var confirmationScore = evaluation.TotalScore * 100m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "Confirmations",
            RawScore = confirmationScore,
            Weight = WeightConfirmations * scaleFactor,
            Reason = $"{evaluation.PassedCount}/{evaluation.TotalCount} confirmations passed ({evaluation.TotalScore:P0})"
        });

        // 3. Volume (15% base, 12.75% with ML)
        var volumeResult = evaluation.Confirmations.FirstOrDefault(c => c.Name == "Volume");
        var volumeScore = volumeResult?.Passed == true ? 100m : 0m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "Volume",
            RawScore = volumeScore,
            Weight = WeightVolume * scaleFactor,
            Reason = volumeResult?.Reason ?? "Not evaluated"
        });

        // 4. R:R ratio (15% base, 12.75% with ML)
        var riskReward = ComputeRiskReward(evaluation.Direction, entryPrice, stopPrice, targetPrice);
        var rrScore = ScoreRiskReward(riskReward);
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "RiskReward",
            RawScore = rrScore,
            Weight = WeightRiskReward * scaleFactor,
            Reason = riskReward > 0
                ? $"R:R = {riskReward:F2} ({DescribeRR(riskReward)})"
                : "Invalid R:R (stop beyond entry or zero risk)"
        });

        // 5. History (10% base, 8.5% with ML)
        var historyScore = historicalWinRate ?? 50m; // neutral if unknown
        historyScore = Math.Clamp(historyScore, 0m, 100m);
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "History",
            RawScore = historyScore,
            Weight = WeightHistory * scaleFactor,
            Reason = historicalWinRate.HasValue
                ? $"Historical win rate: {historicalWinRate.Value:F1}%"
                : "No historical data, using neutral 50%"
        });

        // 6. Volatility (10% base, 8.5% with ML)
        var volResult = evaluation.Confirmations.FirstOrDefault(c => c.Name == "Volatility");
        var volScore = volResult?.Passed == true ? 100m : 0m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "Volatility",
            RawScore = volScore,
            Weight = WeightVolatility * scaleFactor,
            Reason = volResult?.Reason ?? "Not evaluated"
        });

        // 7. ML Confidence (15% when available, 0% when not)
        if (mlConfidence.HasValue)
        {
            var mlScore = (decimal)Math.Clamp(mlConfidence.Value, 0f, 1f) * 100m;
            breakdown.Add(new GradeBreakdownEntry
            {
                Factor = "MLConfidence",
                RawScore = mlScore,
                Weight = WeightMlConfidence,
                Reason = $"ML model prediction: {mlConfidence.Value:P1} win probability"
            });
        }

        // Compute final score
        var finalScore = breakdown.Sum(b => b.WeightedScore);
        var grade = AssignGrade(finalScore);

        return new SignalReport
        {
            Symbol = evaluation.Symbol,
            Date = evaluation.Date,
            Direction = evaluation.Direction,
            Grade = grade,
            Score = Math.Round(finalScore, 2),
            Breakdown = breakdown,
            EntryPrice = entryPrice,
            StopPrice = stopPrice,
            TargetPrice = targetPrice,
            RiskRewardRatio = riskReward,
            HistoricalWinRate = historicalWinRate,
            MlConfidence = mlConfidence,
            Evaluation = evaluation
        };
    }

    /// <summary>
    /// Assign grade based on score thresholds.
    /// </summary>
    public static SignalGrade AssignGrade(decimal score) => score switch
    {
        >= 90m => SignalGrade.A,
        >= 75m => SignalGrade.B,
        >= 60m => SignalGrade.C,
        >= 40m => SignalGrade.D,
        _ => SignalGrade.F
    };

    /// <summary>
    /// Compute risk-to-reward ratio. Returns 0 if invalid (zero risk).
    /// </summary>
    internal static decimal ComputeRiskReward(
        SignalDirection direction, decimal entry, decimal stop, decimal target)
    {
        decimal risk, reward;

        if (direction == SignalDirection.Long)
        {
            risk = entry - stop;     // positive if stop below entry
            reward = target - entry;  // positive if target above entry
        }
        else
        {
            risk = stop - entry;     // positive if stop above entry
            reward = entry - target;  // positive if target below entry
        }

        if (risk <= 0) return 0;
        return reward / risk;
    }

    /// <summary>
    /// Score the R:R ratio on a 0-100 scale.
    /// </summary>
    internal static decimal ScoreRiskReward(decimal rr) => rr switch
    {
        <= 0 => 0m,
        < 1.0m => 25m,
        < 1.5m => 40m,
        < 2.0m => 60m,
        < 2.5m => 75m,
        < 3.0m => 85m,
        _ => 100m // 3:1 or better
    };

    private static string DescribeRR(decimal rr) => rr switch
    {
        >= 3.0m => "excellent",
        >= 2.0m => "good",
        >= 1.5m => "acceptable",
        >= 1.0m => "marginal",
        _ => "poor"
    };
}
