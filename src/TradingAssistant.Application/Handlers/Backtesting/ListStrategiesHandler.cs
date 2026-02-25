using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class ListStrategiesHandler
{
    public static async Task<PagedResponse<StrategyDto>> HandleAsync(
        ListStrategiesQuery query,
        BacktestDbContext db)
    {
        var strategiesQuery = db.Strategies
            .Include(s => s.Rules)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await strategiesQuery.CountAsync();

        var items = await strategiesQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var dtos = items.Select(s => new StrategyDto(
            s.Id, s.Name, s.Description, s.IsActive,
            s.Rules.Select(r => new StrategyRuleDto(
                r.IndicatorType.ToString(), r.Condition,
                r.Threshold, r.SignalType.ToString())).ToList(),
            s.CreatedAt)).ToList();

        return new PagedResponse<StrategyDto>
        {
            Items = dtos,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }
}
