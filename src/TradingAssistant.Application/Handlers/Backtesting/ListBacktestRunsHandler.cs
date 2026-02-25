using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class ListBacktestRunsHandler
{
    public static async Task<PagedResponse<BacktestRunDto>> HandleAsync(
        ListBacktestRunsQuery query,
        BacktestDbContext db)
    {
        var runsQuery = db.BacktestRuns
            .Include(r => r.Strategy)
            .AsQueryable();

        if (query.StrategyId.HasValue)
            runsQuery = runsQuery.Where(r => r.StrategyId == query.StrategyId.Value);

        runsQuery = runsQuery.OrderByDescending(r => r.CreatedAt);

        var totalCount = await runsQuery.CountAsync();

        var items = await runsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new BacktestRunDto(
                r.Id, r.StrategyId, r.Strategy.Name,
                r.Symbol, r.Status.ToString(),
                r.StartDate, r.EndDate, r.CreatedAt))
            .ToListAsync();

        return new PagedResponse<BacktestRunDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
