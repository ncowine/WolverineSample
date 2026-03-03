using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetTournamentEntriesHandler
{
    public static async Task<IReadOnlyList<TournamentEntryDto>> HandleAsync(
        GetTournamentEntriesQuery query,
        IntelligenceDbContext db)
    {
        var entries = await db.TournamentEntries
            .Where(e => e.TournamentRunId == query.TournamentRunId)
            .OrderByDescending(e => e.TotalReturn)
            .ToListAsync();

        return entries.Select(e => new TournamentEntryDto(
            e.Id, e.TournamentRunId, e.StrategyId, e.PaperAccountId,
            e.MarketCode, e.StartDate, e.DaysActive, e.TotalTrades,
            e.WinRate, e.SharpeRatio, e.MaxDrawdown, e.TotalReturn,
            e.Status.ToString(), e.AllocationPercent)).ToList();
    }
}
