using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.MarketData;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.MarketData;

public class InitiateBackfillHandler
{
    public static async Task<BackfillJobDto> HandleAsync(
        BackfillCommand command,
        MarketDataDbContext db,
        ILogger<InitiateBackfillHandler> logger)
    {
        var universe = await db.StockUniverses.FindAsync(command.UniverseId);
        if (universe is null)
            throw new InvalidOperationException($"Universe '{command.UniverseId}' not found.");

        var symbols = universe.GetSymbolList();
        if (universe.IncludesBenchmark && !symbols.Contains("SPY", StringComparer.OrdinalIgnoreCase))
            symbols.Add("SPY");

        var job = new BackfillJob
        {
            UniverseId = command.UniverseId,
            YearsBack = command.YearsBack,
            IsIncremental = command.Incremental,
            Status = BackfillStatus.Pending,
            TotalSymbols = symbols.Count
        };

        db.BackfillJobs.Add(job);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Backfill job {JobId} created for universe '{Universe}' ({Count} symbols, {Years}y, incremental={Incr})",
            job.Id, universe.Name, symbols.Count, command.YearsBack, command.Incremental);

        return BackfillMappers.MapToDto(job);
    }
}

public class GetBackfillStatusHandler
{
    public static async Task<BackfillJobDto?> HandleAsync(
        GetBackfillStatusQuery query,
        MarketDataDbContext db)
    {
        var job = await db.BackfillJobs.FindAsync(query.JobId);
        return job is null ? null : BackfillMappers.MapToDto(job);
    }
}

public class GetBackfillJobsHandler
{
    public static async Task<IReadOnlyList<BackfillJobDto>> HandleAsync(
        GetBackfillJobsQuery query,
        MarketDataDbContext db)
    {
        var jobsQuery = db.BackfillJobs.AsQueryable();

        if (query.UniverseId.HasValue)
            jobsQuery = jobsQuery.Where(j => j.UniverseId == query.UniverseId.Value);

        var jobs = await jobsQuery
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync();

        return jobs.Select(BackfillMappers.MapToDto).ToList();
    }
}

internal static class BackfillMappers
{
    internal static BackfillJobDto MapToDto(BackfillJob j) => new(
        Id: j.Id,
        UniverseId: j.UniverseId,
        YearsBack: j.YearsBack,
        IsIncremental: j.IsIncremental,
        Status: j.Status.ToString(),
        TotalSymbols: j.TotalSymbols,
        CompletedSymbols: j.CompletedSymbols,
        FailedSymbols: j.FailedSymbols,
        ErrorLog: j.ErrorLog,
        CreatedAt: j.CreatedAt,
        StartedAt: j.StartedAt,
        CompletedAt: j.CompletedAt);
}
