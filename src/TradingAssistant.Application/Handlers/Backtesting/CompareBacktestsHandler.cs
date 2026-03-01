using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class CompareBacktestsHandler
{
    public static async Task<BacktestComparisonDto> HandleAsync(
        CompareBacktestsQuery query,
        BacktestDbContext db)
    {
        if (query.BacktestRunIds.Count == 0)
            return new BacktestComparisonDto(new List<BacktestComparisonEntry>());

        if (query.BacktestRunIds.Count > 10)
            throw new InvalidOperationException("Cannot compare more than 10 backtests at once.");

        var runs = await db.BacktestRuns
            .Include(r => r.Result)
            .Include(r => r.Strategy)
            .Where(r => query.BacktestRunIds.Contains(r.Id) && r.Result != null)
            .ToListAsync();

        var entries = runs.Select(r => new BacktestComparisonEntry(
            r.Id,
            r.Symbol,
            r.Strategy.Name,
            r.StartDate,
            r.EndDate,
            r.Result!.TotalTrades,
            r.Result.WinRate,
            r.Result.TotalReturn,
            r.Result.Cagr,
            r.Result.SharpeRatio,
            r.Result.SortinoRatio,
            r.Result.CalmarRatio,
            r.Result.MaxDrawdown,
            r.Result.ProfitFactor,
            r.Result.Expectancy,
            r.Result.OverfittingScore,
            r.Result.SpyComparisonJson,
            r.Result.ParametersJson
        )).ToList();

        return new BacktestComparisonDto(entries);
    }
}
