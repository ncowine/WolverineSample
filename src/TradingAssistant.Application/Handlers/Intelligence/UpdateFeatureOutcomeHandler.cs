using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Updates FeatureSnapshot outcome when a position closes.
/// Triggered by PositionClosed event via Wolverine cascade.
/// </summary>
public class UpdateFeatureOutcomeHandler
{
    public static async Task HandleAsync(
        PositionClosed @event,
        TradingDbContext tradingDb,
        IntelligenceDbContext intelDb,
        ILogger<UpdateFeatureOutcomeHandler> logger)
    {
        try
        {
            // Find the position to get entry price for PnL% calculation
            var position = await tradingDb.Positions.FindAsync(@event.PositionId);
            if (position is null)
            {
                logger.LogWarning("Position {PositionId} not found for outcome update", @event.PositionId);
                return;
            }

            // Find pending feature snapshots for this symbol + account
            // Match by looking up orders for this position's account and symbol
            var pendingSnapshots = await intelDb.FeatureSnapshots
                .Where(s => s.Symbol == @event.Symbol
                            && s.TradeOutcome == TradeOutcome.Pending)
                .OrderByDescending(s => s.CapturedAt)
                .ToListAsync();

            if (pendingSnapshots.Count == 0)
            {
                logger.LogDebug("No pending feature snapshots for {Symbol}", @event.Symbol);
                return;
            }

            var pnlPercent = position.AverageEntryPrice > 0
                ? (@event.ExitPrice - position.AverageEntryPrice) / position.AverageEntryPrice * 100m
                : 0m;

            var outcome = @event.PnL > 0 ? TradeOutcome.Win : TradeOutcome.Loss;

            // Update the most recent pending snapshot for this symbol
            var snapshot = pendingSnapshots.First();
            snapshot.TradeOutcome = outcome;
            snapshot.TradePnlPercent = pnlPercent;
            snapshot.OutcomeUpdatedAt = DateTime.UtcNow;

            await intelDb.SaveChangesAsync();

            logger.LogInformation(
                "Feature outcome updated for {Symbol}: {Outcome} ({PnlPct:F2}%)",
                @event.Symbol, outcome, pnlPercent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to update feature outcome for {Symbol} position {PositionId}",
                @event.Symbol, @event.PositionId);
        }
    }
}
