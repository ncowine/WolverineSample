using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class ClearBacktestResultsHandler
{
    public static async Task<int> HandleAsync(
        ClearBacktestResultsCommand command,
        BacktestDbContext db,
        ILogger<ClearBacktestResultsHandler> logger)
    {
        // Delete results first (FK dependency), then runs
        var resultsDeleted = await db.BacktestResults.ExecuteDeleteAsync();
        var runsDeleted = await db.BacktestRuns.ExecuteDeleteAsync();

        var total = resultsDeleted + runsDeleted;

        logger.LogInformation(
            "[BacktestDb] Cleared all backtest data: {Results} results, {Runs} runs",
            resultsDeleted, runsDeleted);

        return total;
    }
}
