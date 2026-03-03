using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetTournamentHandler
{
    public static async Task<TournamentRunDto?> HandleAsync(
        GetTournamentQuery query,
        IntelligenceDbContext db)
    {
        var run = await db.TournamentRuns
            .FirstOrDefaultAsync(t => t.Id == query.TournamentRunId);

        if (run is null)
            return null;

        return new TournamentRunDto(
            run.Id, run.MarketCode, run.StartDate, run.EndDate,
            run.Status.ToString(), run.MaxEntries, run.EntryCount, run.Description);
    }
}
