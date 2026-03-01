using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Application.Handlers.Screening;

public class GetScreenerHistoryHandler
{
    public static async Task<PagedResponse<ScreenerRunDto>> HandleAsync(
        GetScreenerHistoryQuery query,
        MarketDataDbContext db)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = await db.ScreenerRuns.CountAsync();

        var runs = await db.ScreenerRuns
            .OrderByDescending(r => r.ScanDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ScreenerRunDto(
                r.Id, r.ScanDate, r.StrategyName,
                r.SymbolsScanned, r.SignalsFound, r.CreatedAt))
            .ToListAsync();

        return new PagedResponse<ScreenerRunDto>
        {
            Items = runs,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
