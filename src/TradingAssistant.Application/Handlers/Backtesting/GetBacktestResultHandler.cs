using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Infrastructure.Caching;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class GetBacktestResultHandler
{
    public static async Task<BacktestResultDto> HandleAsync(
        GetBacktestResultQuery query,
        BacktestResultCache cache)
    {
        var result = await cache.Get(query.BacktestRunId);
        if (result is null)
            throw new InvalidOperationException($"Backtest run '{query.BacktestRunId}' not found or has no results yet.");
        return result;
    }
}
