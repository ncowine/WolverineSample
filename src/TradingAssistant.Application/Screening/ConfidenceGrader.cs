namespace TradingAssistant.Application.Screening;

/// <summary>
/// Grades a trade signal from A-F using weighted multi-factor scoring.
///
/// Factor weights (from sprint plan):
///   TF alignment:  25%
///   Confirmations: 25%
///   Volume:        15%
///   R:R ratio:     15%
///   History:       10%
///   Volatility:    10%
///
/// Each factor scores 0-100, then weighted sum produces final 0-100 score.
/// Grade: A (90+), B (75-89), C (60-74), D (40-59), F (&lt;40).
/// </summary>
public static class ConfidenceGrader
{
    // Factor weights (must sum to 1.0)
    private const decimal WeightTrendAlignment = 0.25m;
    private const decimal WeightConfirmations = 0.25m;
    private const decimal WeightVolume = 0.15m;
    private const decimal WeightRiskReward = 0.15m;
    private const decimal WeightHistory = 0.10m;
    private const decimal WeightVolatility = 0.10m;

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
    public static SignalReport Grade(
        SignalEvaluation evaluation,
        decimal entryPrice,
        decimal stopPrice,
        decimal targetPrice,
        decimal? historicalWinRate = null)
    {
        var breakdown = new List<GradeBreakdownEntry>();

        // 1. TF alignment (25%)
        var trendResult = evaluation.Confirmations.FirstOrDefault(c => c.Name == "TrendAlignment");
        var trendScore = trendResult?.Passed == true ? 100m : 0m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "TrendAlignment",
            RawScore = trendScore,
            Weight = WeightTrendAlignment,
            Reason = trendResult?.Reason ?? "Not evaluated"
        });

        // 2. Confirmations (25%) — overall pass rate
        var confirmationScore = evaluation.TotalScore * 100m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "Confirmations",
            RawScore = confirmationScore,
            Weight = WeightConfirmations,
            Reason = $"{evaluation.PassedCount}/{evaluation.TotalCount} confirmations passed ({evaluation.TotalScore:P0})"
        });

        // 3. Volume (15%)
        var volumeResult = evaluation.Confirmations.FirstOrDefault(c => c.Name == "Volume");
        var volumeScore = volumeResult?.Passed == true ? 100m : 0m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "Volume",
            RawScore = volumeScore,
            Weight = WeightVolume,
            Reason = volumeResult?.Reason ?? "Not evaluated"
        });

        // 4. R:R ratio (15%)
        var riskReward = ComputeRiskReward(evaluation.Direction, entryPrice, stopPrice, targetPrice);
        var rrScore = ScoreRiskReward(riskReward);
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "RiskReward",
            RawScore = rrScore,
            Weight = WeightRiskReward,
            Reason = riskReward > 0
                ? $"R:R = {riskReward:F2} ({DescribeRR(riskReward)})"
                : "Invalid R:R (stop beyond entry or zero risk)"
        });

        // 5. History (10%)
        var historyScore = historicalWinRate ?? 50m; // neutral if unknown
        historyScore = Math.Clamp(historyScore, 0m, 100m);
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "History",
            RawScore = historyScore,
            Weight = WeightHistory,
            Reason = historicalWinRate.HasValue
                ? $"Historical win rate: {historicalWinRate.Value:F1}%"
                : "No historical data, using neutral 50%"
        });

        // 6. Volatility (10%)
        var volResult = evaluation.Confirmations.FirstOrDefault(c => c.Name == "Volatility");
        var volScore = volResult?.Passed == true ? 100m : 0m;
        breakdown.Add(new GradeBreakdownEntry
        {
            Factor = "Volatility",
            RawScore = volScore,
            Weight = WeightVolatility,
            Reason = volResult?.Reason ?? "Not evaluated"
        });

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
