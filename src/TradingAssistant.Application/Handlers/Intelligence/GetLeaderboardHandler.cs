using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetLeaderboardHandler
{
    public static async Task<IReadOnlyList<LeaderboardEntryDto>> HandleAsync(
        GetLeaderboardQuery query,
        IntelligenceDbContext db)
    {
        var entries = await db.TournamentEntries
            .Where(e => e.MarketCode == query.MarketCode)
            .OrderByDescending(e => e.SharpeRatio)
            .ToListAsync();

        return entries.Select(e => new LeaderboardEntryDto(
            EntryId: e.Id,
            StrategyId: e.StrategyId,
            StrategyName: e.StrategyName,
            MarketCode: e.MarketCode,
            DaysActive: e.DaysActive,
            TotalTrades: e.TotalTrades,
            WinRate: e.WinRate,
            SharpeRatio: e.SharpeRatio,
            MaxDrawdown: e.MaxDrawdown,
            TotalReturn: e.TotalReturn,
            AllocationPercent: e.AllocationPercent,
            Status: e.Status.ToString(),
            EligibleForPromotion: e.DaysActive >= UpdateTournamentMetricsHandler.MinPromotionDays
        )).ToList();
    }
}
