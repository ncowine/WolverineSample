using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetPipelineStatusHandler
{
    public static async Task<IReadOnlyList<PipelineRunStatusDto>> HandleAsync(
        GetPipelineStatusQuery query,
        IntelligenceDbContext db)
    {
        // Get the latest run date per market
        var latestRuns = db.PipelineRunLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.MarketCode))
            latestRuns = latestRuns.Where(l => l.MarketCode == query.MarketCode);

        var grouped = await latestRuns
            .GroupBy(l => l.MarketCode)
            .Select(g => new { MarketCode = g.Key, LatestRunDate = g.Max(l => l.RunDate) })
            .ToListAsync();

        var results = new List<PipelineRunStatusDto>();

        foreach (var market in grouped.OrderBy(g => g.MarketCode))
        {
            var steps = await db.PipelineRunLogs
                .Where(l => l.MarketCode == market.MarketCode && l.RunDate == market.LatestRunDate)
                .OrderBy(l => l.StepOrder)
                .ToListAsync();

            var stepDtos = steps.Select(s => new PipelineStepStatusDto(
                s.StepName,
                s.StepOrder,
                s.Status.ToString(),
                s.Duration.TotalSeconds,
                s.ErrorMessage,
                s.RetryCount)).ToList();

            var completed = steps.Count(s => s.Status == Domain.Intelligence.Enums.PipelineStepStatus.Completed);
            var failed = steps.Count(s => s.Status == Domain.Intelligence.Enums.PipelineStepStatus.Failed);
            var skipped = steps.Count(s => s.Status == Domain.Intelligence.Enums.PipelineStepStatus.Skipped);

            var overallStatus = failed > 0 ? "PartialFailure"
                : skipped > 0 ? "CompletedWithSkips"
                : "Completed";

            results.Add(new PipelineRunStatusDto(
                market.MarketCode,
                market.LatestRunDate,
                overallStatus,
                completed,
                failed,
                skipped,
                steps.Count,
                stepDtos));
        }

        return results;
    }
}
