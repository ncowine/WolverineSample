using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class GetBacktestResultHandler
{
    public static async Task<BacktestResultDto> HandleAsync(
        GetBacktestResultQuery query,
        BacktestDbContext db)
    {
        var run = await db.BacktestRuns
            .Include(r => r.Result)
            .FirstOrDefaultAsync(r => r.Id == query.BacktestRunId);

        if (run is null)
            throw new InvalidOperationException($"Backtest run '{query.BacktestRunId}' not found.");

        return RunBacktestHandler.MapToDto(run, run.Result);
    }
}
