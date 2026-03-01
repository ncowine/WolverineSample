using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class GetOptimizedParamsHandler
{
    public static async Task<OptimizedParamsResponse> HandleAsync(
        GetOptimizedParamsQuery query,
        BacktestDbContext db)
    {
        var allSets = await db.OptimizedParameterSets
            .Where(p => p.StrategyId == query.StrategyId)
            .OrderByDescending(p => p.Version)
            .ToListAsync();

        var current = allSets.FirstOrDefault(p => p.IsActive);
        var history = allSets.Select(SaveOptimizedParamsHandler.MapToDto).ToList();

        return new OptimizedParamsResponse(
            current != null ? SaveOptimizedParamsHandler.MapToDto(current) : null,
            history);
    }
}
