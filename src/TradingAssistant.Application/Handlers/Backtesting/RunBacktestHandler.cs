using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Events;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public class RunBacktestHandler
{
    public static async Task<(LoadHistoricalData, BacktestRunDto)> HandleAsync(
        RunBacktestCommand command,
        BacktestDbContext db,
        ILogger<RunBacktestHandler> logger)
    {
        var strategy = await db.Strategies
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == command.StrategyId)
            ?? throw new InvalidOperationException($"Strategy '{command.StrategyId}' not found.");

        var run = new BacktestRun
        {
            StrategyId = command.StrategyId,
            Symbol = command.Symbol,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Status = BacktestRunStatus.Pending
        };

        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync();

        logger.LogInformation("[BacktestDb] Backtest run {RunId} created for strategy '{Name}' on {Symbol}",
            run.Id, strategy.Name, command.Symbol);

        var loadCmd = new LoadHistoricalData(
            run.Id, strategy.Id, command.Symbol,
            command.StartDate, command.EndDate);

        var dto = new BacktestRunDto(
            run.Id, strategy.Id, strategy.Name,
            run.Symbol, run.Status.ToString(),
            run.StartDate, run.EndDate, run.CreatedAt);

        return (loadCmd, dto);
    }
}
