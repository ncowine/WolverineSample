using Microsoft.EntityFrameworkCore;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Evaluates whether ML model retraining is needed for a market.
/// Used by MlRetrainService (BackgroundService) to check trigger conditions.
/// </summary>
public static class MlRetrainChecker
{
    public const int MonthlyRetrainDays = 30;
    public const int NewTradeThreshold = 50;

    /// <summary>
    /// Check if retraining should be triggered for a market.
    /// Returns (shouldRetrain, reason).
    /// </summary>
    public static async Task<(bool Trigger, string Reason)> ShouldRetrain(
        IntelligenceDbContext db, string marketCode, CancellationToken ct = default)
    {
        // Get latest model for this market
        var latestModel = await db.MlModels
            .Where(m => m.MarketCode == marketCode)
            .OrderByDescending(m => m.ModelVersion)
            .FirstOrDefaultAsync(ct);

        if (latestModel is null)
        {
            // No model exists — check if there are enough labeled snapshots
            var labeledCount = await db.FeatureSnapshots
                .CountAsync(s => s.MarketCode == marketCode
                    && s.TradeOutcome != TradeOutcome.Pending, ct);

            return labeledCount >= 20
                ? (true, $"No model exists, {labeledCount} labeled snapshots available")
                : (false, "Insufficient data for initial training");
        }

        // Check 1: Monthly retrain (30+ days since last training)
        var daysSinceTraining = (DateTime.UtcNow - latestModel.TrainedAt).TotalDays;
        if (daysSinceTraining >= MonthlyRetrainDays)
        {
            return (true, $"Monthly retrain: {daysSinceTraining:F0} days since last training");
        }

        // Check 2: New trade threshold (50+ new labeled snapshots since last model)
        var newLabeledCount = await db.FeatureSnapshots
            .CountAsync(s => s.MarketCode == marketCode
                && s.TradeOutcome != TradeOutcome.Pending
                && s.CapturedAt > latestModel.TrainedAt, ct);

        if (newLabeledCount >= NewTradeThreshold)
        {
            return (true, $"Trade threshold: {newLabeledCount} new closed trades since v{latestModel.ModelVersion}");
        }

        return (false, "No retrain criteria met");
    }
}
