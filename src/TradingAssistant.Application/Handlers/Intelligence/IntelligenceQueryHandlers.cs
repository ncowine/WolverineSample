using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class GetCurrentRegimeHandler
{
    public static async Task<MarketRegimeDto> HandleAsync(
        GetCurrentRegimeQuery query,
        IntelligenceDbContext db)
    {
        var regime = await db.MarketRegimes
            .Where(r => r.MarketCode == query.MarketCode)
            .OrderByDescending(r => r.ClassifiedAt)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"No regime classification found for '{query.MarketCode}'.");

        return MapRegimeToDto(regime);
    }

    internal static MarketRegimeDto MapRegimeToDto(MarketRegime r) =>
        new(r.Id, r.MarketCode, r.CurrentRegime.ToString(), r.RegimeStartDate,
            r.RegimeDuration, r.SmaSlope50, r.SmaSlope200, r.VixLevel,
            r.BreadthScore, r.PctAbove200Sma, r.AdvanceDeclineRatio,
            r.ConfidenceScore, r.ClassifiedAt);
}

public class GetRegimeHistoryHandler
{
    public static async Task<PagedResponse<MarketRegimeDto>> HandleAsync(
        GetRegimeHistoryQuery query,
        IntelligenceDbContext db)
    {
        var baseQuery = db.MarketRegimes
            .Where(r => r.MarketCode == query.MarketCode)
            .OrderByDescending(r => r.ClassifiedAt);

        var totalCount = await baseQuery.CountAsync();

        var items = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResponse<MarketRegimeDto>
        {
            Items = items.Select(GetCurrentRegimeHandler.MapRegimeToDto).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}

public class GetLatestBreadthHandler
{
    public static async Task<BreadthSnapshotDto> HandleAsync(
        GetLatestBreadthQuery query,
        IntelligenceDbContext db)
    {
        var snapshot = await db.BreadthSnapshots
            .Where(b => b.MarketCode == query.MarketCode)
            .OrderByDescending(b => b.SnapshotDate)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                $"No breadth snapshot found for '{query.MarketCode}'.");

        return new BreadthSnapshotDto(
            snapshot.Id, snapshot.MarketCode, snapshot.SnapshotDate,
            snapshot.AdvanceDeclineRatio, snapshot.PctAbove200Sma, snapshot.PctAbove50Sma,
            snapshot.NewHighs, snapshot.NewLows, snapshot.TotalStocks,
            snapshot.Advancing, snapshot.Declining);
    }
}

public class GetCorrelationMatrixHandler
{
    public static async Task<CorrelationMatrixDto> HandleAsync(
        GetCorrelationMatrixQuery query,
        IntelligenceDbContext db)
    {
        var snapshot = await db.CorrelationSnapshots
            .OrderByDescending(c => c.SnapshotDate)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("No correlation matrix available.");

        return new CorrelationMatrixDto(
            snapshot.Id, snapshot.SnapshotDate,
            snapshot.LookbackDays, snapshot.MatrixJson);
    }
}
