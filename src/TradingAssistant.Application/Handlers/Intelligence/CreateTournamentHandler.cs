using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class CreateTournamentHandler
{
    public const int MinEntries = 2;
    public const int MaxEntriesLimit = 20;

    public static async Task<TournamentRunDto> HandleAsync(
        CreateTournamentCommand command,
        IntelligenceDbContext db,
        ILogger<CreateTournamentHandler> logger)
    {
        var maxEntries = Math.Clamp(command.MaxEntries, MinEntries, MaxEntriesLimit);

        var run = new TournamentRun
        {
            MarketCode = command.MarketCode,
            StartDate = DateTime.UtcNow,
            Status = TournamentRunStatus.Active,
            MaxEntries = maxEntries,
            Description = command.Description,
            EntryCount = 0
        };

        db.TournamentRuns.Add(run);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Created tournament run {TournamentId} for market {Market} with max {Max} entries",
            run.Id, run.MarketCode, run.MaxEntries);

        return new TournamentRunDto(
            run.Id, run.MarketCode, run.StartDate, run.EndDate,
            run.Status.ToString(), run.MaxEntries, run.EntryCount, run.Description);
    }
}
