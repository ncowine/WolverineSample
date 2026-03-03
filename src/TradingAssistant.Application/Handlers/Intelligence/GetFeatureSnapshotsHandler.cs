using Microsoft.EntityFrameworkCore;
using TradingAssistant.Application.Handlers.Intelligence;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetFeatureSnapshotsHandler
{
    public static async Task<PagedResponse<FeatureSnapshotDto>> HandleAsync(
        GetFeatureSnapshotsQuery query,
        IntelligenceDbContext intelDb)
    {
        var dbQuery = intelDb.FeatureSnapshots.AsQueryable();

        if (!string.IsNullOrEmpty(query.Symbol))
            dbQuery = dbQuery.Where(s => s.Symbol == query.Symbol);

        if (!string.IsNullOrEmpty(query.MarketCode))
            dbQuery = dbQuery.Where(s => s.MarketCode == query.MarketCode);

        if (!string.IsNullOrEmpty(query.Outcome) &&
            Enum.TryParse<TradeOutcome>(query.Outcome, true, out var outcome))
            dbQuery = dbQuery.Where(s => s.TradeOutcome == outcome);

        var total = await dbQuery.CountAsync();

        var items = await dbQuery
            .OrderByDescending(s => s.CapturedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new FeatureSnapshotDto(
                s.Id, s.TradeId, s.Symbol, s.MarketCode, s.CapturedAt,
                s.FeatureVersion, s.FeatureCount,
                s.TradeOutcome.ToString(), s.TradePnlPercent, s.OutcomeUpdatedAt,
                null))
            .ToListAsync();

        return new PagedResponse<FeatureSnapshotDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
