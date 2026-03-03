using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class PromoteStrategyHandler
{
    public const int MinDaysForPromotion = 30;
    public const decimal MinSharpe = 1.0m;
    public const decimal MaxDrawdownPercent = 10m;
    public const decimal MinWinRate = 0.50m;
    public const decimal InitialAllocation = 25m;

    public static async Task<PromoteStrategyResultDto> HandleAsync(
        PromoteStrategyCommand command,
        IntelligenceDbContext db,
        ILogger<PromoteStrategyHandler> logger)
    {
        var entry = await db.TournamentEntries
            .FirstOrDefaultAsync(e => e.Id == command.EntryId);

        if (entry is null)
        {
            return Fail(Guid.Empty, "", "Tournament entry not found.");
        }

        if (entry.Status == TournamentStatus.Promoted)
        {
            return Fail(entry.Id, entry.StrategyName, "Strategy is already promoted.");
        }

        if (entry.Status == TournamentStatus.Retired)
        {
            return Fail(entry.Id, entry.StrategyName, "Cannot promote a retired strategy.");
        }

        // Validate promotion criteria (unless force override)
        if (!command.Force)
        {
            var failures = ValidatePromotionCriteria(entry.DaysActive, entry.SharpeRatio,
                entry.MaxDrawdown, entry.WinRate);

            if (failures.Count > 0)
            {
                var reason = string.Join("; ", failures);
                return Fail(entry.Id, entry.StrategyName,
                    $"Does not meet promotion criteria: {reason}");
            }
        }

        // Promote
        entry.Status = TournamentStatus.Promoted;
        entry.PromotedAt = DateTime.UtcNow;
        entry.AllocationPercent = InitialAllocation;

        await db.SaveChangesAsync();

        var promotionReason = command.Force
            ? "Manual promotion override"
            : $"Met all criteria: {entry.DaysActive} days, Sharpe {entry.SharpeRatio:F2}, DD {entry.MaxDrawdown:F1}%, WR {entry.WinRate:P0}";

        logger.LogInformation(
            "Strategy '{Strategy}' promoted in {Market}: {Reason}",
            entry.StrategyName, entry.MarketCode, promotionReason);

        return new PromoteStrategyResultDto(
            Success: true,
            EntryId: entry.Id,
            StrategyName: entry.StrategyName,
            AllocationPercent: InitialAllocation,
            Reason: promotionReason);
    }

    internal static List<string> ValidatePromotionCriteria(
        int daysActive, decimal sharpeRatio, decimal maxDrawdown, decimal winRate)
    {
        var failures = new List<string>();

        if (daysActive < MinDaysForPromotion)
            failures.Add($"Days active {daysActive} < {MinDaysForPromotion}");

        if (sharpeRatio < MinSharpe)
            failures.Add($"Sharpe {sharpeRatio:F2} < {MinSharpe}");

        if (maxDrawdown > MaxDrawdownPercent)
            failures.Add($"Max drawdown {maxDrawdown:F1}% > {MaxDrawdownPercent}%");

        if (winRate < MinWinRate)
            failures.Add($"Win rate {winRate:P0} < {MinWinRate:P0}");

        return failures;
    }

    private static PromoteStrategyResultDto Fail(Guid entryId, string name, string error)
    {
        return new PromoteStrategyResultDto(
            Success: false, EntryId: entryId, StrategyName: name,
            AllocationPercent: 0, Error: error);
    }
}
