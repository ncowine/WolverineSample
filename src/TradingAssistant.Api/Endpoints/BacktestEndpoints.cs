using Microsoft.AspNetCore.Authorization;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.SharedKernel;
using Wolverine;
using Wolverine.Http;

namespace TradingAssistant.Api.Endpoints;

public static class BacktestEndpoints
{
    [Authorize]
    [WolverinePost("/api/strategies")]
    public static async Task<StrategyDto> CreateStrategy(CreateStrategyCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<StrategyDto>(command);
    }

    [Authorize]
    [WolverineGet("/api/strategies")]
    public static async Task<PagedResponse<StrategyDto>> ListStrategies(
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<StrategyDto>>(
            new ListStrategiesQuery(page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }

    [Authorize]
    [WolverinePost("/api/backtests")]
    public static async Task<BacktestRunDto> RunBacktest(RunBacktestCommand command, IMessageBus bus)
    {
        return await bus.InvokeAsync<BacktestRunDto>(command);
    }

    [Authorize]
    [WolverineGet("/api/backtests/{backtestRunId}")]
    public static async Task<BacktestResultDto> GetBacktestResult(Guid backtestRunId, IMessageBus bus)
    {
        return await bus.InvokeAsync<BacktestResultDto>(new GetBacktestResultQuery(backtestRunId));
    }

    [Authorize]
    [WolverineGet("/api/backtests")]
    public static async Task<PagedResponse<BacktestRunDto>> ListBacktestRuns(
        Guid? strategyId,
        int page,
        int pageSize,
        IMessageBus bus)
    {
        return await bus.InvokeAsync<PagedResponse<BacktestRunDto>>(
            new ListBacktestRunsQuery(strategyId, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
    }
}
