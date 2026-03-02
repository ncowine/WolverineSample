using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Handles RegimeChanged events by selecting the best strategy for the new regime.
/// Reads performance scores from IntelligenceDbContext and updates/creates StrategyAssignment.
/// </summary>
public class SelectStrategiesHandler
{
    public static async Task HandleAsync(
        RegimeChanged @event,
        IntelligenceDbContext db,
        ILogger<SelectStrategiesHandler> logger)
    {
        if (!Enum.TryParse<RegimeType>(@event.ToRegime, out var newRegime))
        {
            logger.LogWarning("Unknown regime type '{Regime}' for {Market}", @event.ToRegime, @event.MarketCode);
            return;
        }

        logger.LogInformation("Regime changed for {Market}: {From} → {To}. Selecting best strategy...",
            @event.MarketCode, @event.FromRegime, @event.ToRegime);

        // Check if current assignment is locked
        var current = await db.StrategyAssignments
            .FirstOrDefaultAsync(a => a.MarketCode == @event.MarketCode);

        if (!StrategySelector.ShouldReplace(current, newRegime))
        {
            logger.LogInformation("Strategy for {Market} is locked to '{Strategy}'. Skipping selection.",
                @event.MarketCode, current!.StrategyName);
            return;
        }

        // Get all regime scores for this market and the new regime
        var scores = await db.StrategyRegimeScores
            .Where(s => s.MarketCode == @event.MarketCode)
            .ToListAsync();

        var best = StrategySelector.SelectBest(scores, newRegime);
        if (best is null)
        {
            logger.LogWarning("No strategies with regime scores for {Market}/{Regime}. No assignment made.",
                @event.MarketCode, newRegime);
            return;
        }

        var now = DateTime.UtcNow;

        if (current is not null)
        {
            // Update existing assignment
            current.StrategyId = best.StrategyId;
            current.StrategyName = $"Strategy-{best.StrategyId.ToString("N")[..8]}";
            current.Regime = newRegime;
            current.AllocationPercent = StrategySelector.StartAllocation;
            current.SwitchoverStartDate = now;
            current.AssignedAt = now;
            // IsLocked stays false (we already checked)
        }
        else
        {
            // Create new assignment
            db.StrategyAssignments.Add(new StrategyAssignment
            {
                MarketCode = @event.MarketCode,
                StrategyId = best.StrategyId,
                StrategyName = $"Strategy-{best.StrategyId.ToString("N")[..8]}",
                Regime = newRegime,
                AllocationPercent = StrategySelector.StartAllocation,
                IsLocked = false,
                SwitchoverStartDate = now,
                AssignedAt = now
            });
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Assigned strategy {StrategyId} to {Market} for {Regime} (Sharpe: {Sharpe:F2}, Allocation: {Alloc}%)",
            best.StrategyId, @event.MarketCode, newRegime, best.SharpeRatio, StrategySelector.StartAllocation);
    }
}
