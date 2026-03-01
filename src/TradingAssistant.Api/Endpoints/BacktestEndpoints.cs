using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public class BacktestEndpoints : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        var strategies = app.MapGroup("/api/strategies")
            .WithTags("Strategies")
            .RequireAuthorization();

        strategies.MapPostToWolverine<CreateStrategyCommand, StrategyDto>("/")
            .WithSummary("Create a new trading strategy with rules");

        strategies.MapGet("/", ListStrategies)
            .WithSummary("List all trading strategies (paginated)");

        var backtests = app.MapGroup("/api/backtests")
            .WithTags("Backtests")
            .RequireAuthorization();

        backtests.MapPostToWolverine<RunBacktestCommand, BacktestRunDto>("/")
            .WithSummary("Run a backtest for a strategy against historical data");

        backtests.MapGet("/{backtestRunId}", GetBacktestResult)
            .WithSummary("Get the result of a specific backtest run");

        backtests.MapGet("/", ListBacktestRuns)
            .WithSummary("List backtest runs, optionally filtered by strategy");
    }

    private static async Task<PagedResponse<StrategyDto>> ListStrategies(
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<StrategyDto>>(
            new ListStrategiesQuery(page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    private static async Task<BacktestResultDto> GetBacktestResult(Guid backtestRunId, IMessageBus bus)
    {
        return await bus.InvokeAsync<BacktestResultDto>(new GetBacktestResultQuery(backtestRunId));
    }

    private static async Task<PagedResponse<BacktestRunDto>> ListBacktestRuns(
        Guid? strategyId,
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<BacktestRunDto>>(
            new ListBacktestRunsQuery(strategyId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }
}
